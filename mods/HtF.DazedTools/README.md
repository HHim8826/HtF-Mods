# HtF.DazedTools

把 `DazedCommands` 從「改反編譯源碼」搬到 BepInEx 插件，並加上 IMGUI 操作介面。

## 為什麼要換載體

反編譯出來的 `../../Assembly-CSharp/` **無法整體編譯**（FishNet IL weaving 產生含 `.` 和 `-`
的非法識別字，見 `../../AI_CONTEXT.md` 第 2 節），所以改源碼的成果沒辦法變成能分發的東西。
Harmony patch 完全繞開這件事：只**參照** `Assembly-CSharp.dll`，不重建它。

## 架構

| 檔案 | 作用 |
|---|---|
| `src/Plugin.cs` | BepInEx 進入點、設定項、熱鍵、Harmony 啟動 |
| `src/Patches.cs` | 4 個 Harmony patch（見下） |
| `src/Commands/CommandCore.cs` | 修好的指令實作，從 `DazedCommands.cs` 移植 |
| `src/UI/CommandRegistry.cs` | 指令中繼資料表 —— **UI 完全由它生成** |
| `src/UI/GameData.cs` | 魚餌／配件／口袋／馬達／NPC／賭盤對照表 |
| `src/UI/GameData.Items.cs` | 85 個物品（由手冊第 4.1 節自動產生） |
| `src/UI/Theme.cs` | 深色主題（執行期產生貼圖 + 複製一份 GUISkin） |
| `src/UI/ModWindow.cs` | IMGUI 視窗 |

### 移植方式

`CommandCore.cs` 是 `../../decompiled-current/DazedCommands.cs` 的機械轉換：
剝掉反編譯器的 Token 註解、`MonoBehaviour` → 靜態類別、改名避免與遊戲型別衝突、
移除 `ClientSettings.CheatsEnabled` 閘門（改由插件自己控制）。
**指令邏輯與所有安全防護原封不動**（批次上限 48、彈丸上限 64、搜尋半徑 60m、
`/forceunlockpocket` 與 `/buybaitfree` 的下界檢查、NPC 255 阻擋、賭場單例檢查）。

### Harmony patch

| 目標 | 種類 | 用途 |
|---|---|---|
| `DazedCommands.IsServerCommand` | Prefix | 聊天欄指令改走修好的版本，跳過原實作 |
| `Player.BlockInputs` (getter) | Postfix | 視窗開著時擋掉本機玩家的所有輸入 |
| `ChatManager.ChatMessage(string)` | Postfix | 把遊戲輸出鏡射到視窗的輸出區 |
| `ClientSettings.CheatsEnabled` (getter) | Postfix | 選用：開啟遊戲內建的除錯熱鍵 |

四個目標都用 `nameof` 指定，打錯字會在編譯期就失敗，不會拖到執行期。

### IMGUI 的結構一致性規則（踩過坑）

Unity 每幀對 `OnGUI` 跑**多次**：先 `Layout` 算版面，再跑滑鼠／鍵盤事件，最後 `Repaint`。
若在後面那幾次裡改動了**版面結構**（控件數量、群組巢狀、某塊要不要顯示），
就會和 `Layout` 那次算出的結構對不上，GUILayout 取到 null 直接 NRE
——堆疊會指在當下那個繪製函式裡，很容易誤判成那個函式自己的 bug。

所以 `ModWindow` 的規則是：**會改變結構的狀態變更一律 `Defer()`，只在 `Layout` 事件套用。**
已知會踩到的有：

| 動作 | 為什麼是結構變動 |
|---|---|
| 開／關選單覆蓋層 | 整個視窗換成另一種內容 |
| 切換左側分頁 | 指令清單整份換掉 |
| 選單搜尋框篩選 | 清單項目數量改變 |
| `PosOrVoid` 切模式 | 「座標」模式多出三個輸入框 |
| 輸出區新增／清除訊息 | Label 數量改變 |

另外，參數列會依寬度預算**自動換行**，執行按鈕永遠獨立一列。
參數多的指令（例如 `/hitplayer`：玩家下拉 + 傷害 + 繞過PvP）加起來會超過卡片寬度，
GUILayout 會把排在最後的元素擠出可視範圍——執行按鈕就是這樣整個消失的。

輸出清單與選單篩選結果都在 `Layout` 時取快照（`_logView`、`PickerView`），
其餘事件沿用同一份，確保同一幀內結構不變。
`PosOrVoid` 的模式存在獨立的 `PosModes` 而不是從參數字串反推——
否則使用者把座標刪空時，`hasPos` 會在幀中途翻轉。

### 遊戲端的守衛（為什麼有些指令看起來「沒用」）

這些全部是**遊戲自己的限制**，不是 mod 壞掉。查過原始碼後都已在 UI 上標紅字說明。

| 現象 | 真正的原因 |
|---|---|
| `/setitemmultiplier` 對已有倍率的物品無效 | `Item.SetKillscoreMultiplier` 第二道守衛是 `if (_killScoreMultiplier.Value != 1f) return;`——一個物品只能設一次 |
| `/setitemholder` 搶不到別人手上的東西 | `RpcLogic___SetItemHolder` 裡 `if (syncedHolder && syncedHolder != A_2) { TargetReconcileRejectedItemPickup(...); return; }`。它實際能做的是把**無主**物品塞進某人手裡 |
| `/tpitems` 當房主時完全沒反應 | `RigidbodySync.ServerSetPosRot` 整個方法體包在 `if (netCon != InstanceFinder.ClientManager.Connection)` 裡。那是防回音用的：正常流程送的就是自己的連線。你是房主時條件永遠為假，**整段被跳過** |
| `/hijackitemphysics` 看不出效果 | 它只改 `_syncedSimulator`，本來就沒有視覺變化。而且房主送給自己等於沒變 |
| `/forceplacebet` 選綠沒開出綠 | `ServerStartBet(chosenColor)` 設的是 `_curBetColor`——**你押的顏色**，不是開獎結果。開獎仍然隨機 |
| `/spoofroulette` 沒有實質效果 | `CasinoManager.UpdateGameObjects(pos, angle)` 只廣播輪盤物件的擺放，純視覺欺騙，不影響結果或賠付 |
| `/steerboat` 沒反應 | `Boat.ServerSetInput` 開頭 `if (!IsServerInitialized \|\| !_driver.Value) return;`——沒有駕駛就整段忽略。而且真駕駛每幀都在送輸入（`Boat.cs:721`），單發下一幀就被蓋掉 |

#### 因此改掉的地方

- **`/tpitems` 與 `/hijackitemphysics` 不再送 RPC**，改用公開的
  `RigidbodySync.StartSimulateLocal(pos, rot)` 先接管模擬權再定位——
  這正是遊戲自己搬東西的做法，房主和客戶端兩邊都有效。
  已經是本機在模擬的物品改走 `TeleportToPosRot()`，因為 `StartSimulateLocal`
  在這種情況會提早 return 而不搬動（`pos` 剛好等於 `Vector3.zero` 時也會略過定位）。
- **`/setitemholder` 預設改挑最近的無主物品**，並在目標已被持有時明講會被伺服器退回。
- **`/steerboat` 多了「持續秒數」參數**（預設 3 秒），由 `PumpBoatInput()` 每幀重送蓋過真駕駛。
- **`/addmoney`、`/removemoney` 支援金額參數**（預設仍是 9999）。
- **`/spoofchat` 已移除**。它確實能運作（`SendChatMessage` 是無 `ExcludeServer` 的
  ObserversRpc，連房主自己都收得到），但那純粹是冒名發言的騷擾工具，沒有除錯價值。

### UI 是資料驅動的

`CommandRegistry.All` 裡每個指令宣告自己的分類、風險等級、參數型別。
`ModWindow` 只有一個通用的算繪迴圈——加新指令只要在 registry 加一行，
不用碰 UI 程式碼。參數型別決定控件：`Item` 給可搜尋的 85 項清單、
`Player` 給當前玩家下拉、`Bait`/`Pocket` 自動跳過會自踢的索引 0。

## 建置

```bash
dotnet build
```

建置後會自動複製到 r2modman 的 profile。路徑可覆寫：

```bash
dotnet build -p:GameManaged="D:\...\How to Fish_Data\Managed" -p:ProfileDir="%AppData%\r2modmanPlus-local\HowToFish\profiles\Default"
```

`Managed/` 底下有 `Assembly-CSharp` 的手動備份副本（現在叫 `- 1.0.9`，以前叫 `- 複製`），
csproj 用 `* - *.dll` 型樣排除，不用固定檔名——對方再改一次名也不會載到舊版。

## 使用

1. r2modman 按 **Start modded**
2. 遊戲內按 **Insert**
3. 左側選分類 → 填參數 → 按「執行」

底部有主控台可直接打指令（不用加 `/` 也行），上下鍵按鈕可翻歷史。
聊天欄輸入 `/xxx` 一樣有效。

### 設定（`BepInEx/config/htf.dazedtools.cfg`，第一次執行後產生）

| 項目 | 預設 | 說明 |
|---|---|---|
| 開關鍵 | Insert | |
| 縮放 | 1.0 | 0.6–2.0 |
| 字型 | Microsoft JhengHei UI | 留空則用 Unity 內建字型，中文可能變方塊 |
| 開啟遊戲內建作弊鍵 | false | 開了之後 M/N 加減錢、O 換島、逗號跳教學會生效 |
| 預設解鎖危險指令 | false | |

### 危險指令鎖

標為「危險」的指令（`finishgame`、`sendfinishgame`、成就相關、`spoofprojectile`）
預設不能按，要先勾視窗右上的「解鎖危險指令」。

### 中英雙語

分類、指令說明、警告、參數名稱、下拉選單的項目都有英文版，跟著 `HtF.ConfigMenu`
的「語言」設定走（沒裝 ConfigMenu 就跟著遊戲語系）。指令說明的中英兩份就寫在
`CommandRegistry.cs` 的同一行上，視窗自己的文字在 `src/UI/Localization.cs`。

**送出去的指令字串不受語言影響**——`/spawn tuna`、`me`、`all`、`confirm`
是遊戲的指令語法，不是給人看的文案。共用底層見 `mods/README.md` 的〈中英雙語〉。

## 尚未在遊戲內驗證

以下是編譯通過、但需要實際跑一次才能確認的：

- IMGUI 是否被遊戲的 URP Overlay Canvas蓋住（已先設 `GUI.depth = -1000`）
- `Font.CreateDynamicFontFromOSFont("Microsoft JhengHei UI")` 是否拿得到字型（拿不到會在 log 留警告並退回內建字型）
- 開視窗時 `PlayerCamera.ToggleMouse(true)` 在主選單階段是否會丟例外（已包 try/catch）

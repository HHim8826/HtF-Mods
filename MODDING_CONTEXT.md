# AI 上下文：How to Fish 模組開發

> 這份文件記錄**做 mod 時已驗證的事實**，給後續 AI session 直接使用，不必重新推導。
> 姊妹文件 `AI_CONTEXT.md` 記錄的是反編譯與 ServerRPC 安全研究；兩份不重複。
>
> 每條主張都標註了驗證方式。**未驗證的推測一律標 `[未驗證]`**，不要當事實用。

---

## 1. 環境（已驗證）

| 項目 | 值 |
|---|---|
| Mod loader | BepInEx **5.4.23.5**（Mono），Harmony **2.9**，doorstop 4.5.0 |
| r2modman profile | `%AppData%\r2modmanPlus-local\HowToFish\profiles\Default` |
| 遊戲 Managed | `D:\SteamLibrary\steamapps\common\How to Fish\How to Fish\How to Fish_Data\Managed` |
| Unity | 6000.4.4f1，Mono 後端（有 `MonoBleedingEdge/`），**無反作弊** |
| .NET SDK | 10.0.303 與 6.0.428 都在 |
| dnSpy | `C:\Users\leo\Downloads\dnSpy-net-win64\dnSpy.Console.exe` |
| Thunderstore | 有 `how-to-fish` 社群，BepInExPack 由 r2modman 安裝 |

遊戲同時啟用**舊版與新版 Input System**（`Input.GetKeyDown` 在 `ChatManager`、`MoneyManager`
等多處實際使用，且 `UnityEngine.InputLegacyModule.dll` 存在）。
所以 IMGUI 與 `Input.GetKey*` 熱鍵都可以正常用。

**已驗證可直接參照的組件**：`UnityEngine.UI.dll`（Button）、`Unity.TextMeshPro.dll`（TMP_Text）、
`Unity.Localization.dll`、`UnityEngine.IMGUIModule.dll`。

### 建置

七個 mod 都在 `mods/`，共用 `mods/Common.props` 與 `mods/Shared/*.cs`
（兩者都是編譯期共用，**不是**執行期相依——`Shared` 是用 `<Compile Include>` 編進每個 DLL 的）。
參照是對 `Managed/*.dll` 做萬用字元展開，排除規則見 `AI_CONTEXT.md` 第 2 節
（`netstandard.dll`、`mscorlib`、`System*`、以及 `* - *.dll` 備份副本）。

---

## 2. 遊戲版本（已驗證）

主畫面右下的「遊戲版本 1.0.9」來自 `CanvasManager.cs:16`：

```csharp
this._versionText.text = Application.version ?? "";
```

`Application.version` 出自 `globalgamemanagers` 的 **bundleVersion**（實測值 `1.0.9`），
**與 `Assembly-CSharp.dll` 無關**。

因此：目前安裝的遊戲回報 1.0.9；而 `Managed/` 下的
`Assembly-CSharp.dll` 與備份 `Assembly-CSharp - 1.0.9.dll` 有 **66 個檔案內容不同**
但**沒有新增或移除任何型別**。
`[未驗證]` 兩者何者較新無法從版本字串判斷——很可能是同一版號下的 hotfix 換了 DLL 卻沒動 bundleVersion。

**重點：所有 mod 掛鉤的檔案在這 66 個差異之外**，即兩份完全相同：
`ServerSettings` `PlayerVitals` `MoneyManager` `SaveManager` `Creature`
`BaitInfo` `ItemInfoWeight` `Fishable` `Radio` `RadioChannel`，
以及 `Item.TotalWorth`、`CreatureManager.GetRandomItem` 兩個方法本身。

### 三份反編譯結果

| 資料夾 | 內容 |
|---|---|
| `Assembly-CSharp/` | 舊版（1.0.7 期），含手動改過的 `DazedCommands.cs`。**勿覆蓋** |
| `Assembly-CSharp-1.09/` | `Assembly-CSharp - 1.0.9.dll` 備份的反編譯 |
| `decompiled-current/` | 目前遊戲實際載入的版本 ← **查東西看這份** |

重新產生：見 `AI_CONTEXT.md` 第 1.5 節。

### 1.0.7 → 1.0.9 的主要新增：難度系統

- 新列舉 `Difficulty { Easy, Default, Hard }`
- `ServerSettings` 多了 `_difficulty` SyncVar，以及三個**靜態屬性**
  `Difficulty` / `HealthMultiplier` / `DamageMultiplier`
- `OnDifficultyChange(byte,byte,bool)`（私有）把乘數設成
  簡單 `0.75/0.5`、普通 `1/1`、困難 `1.25/1.25`
- 消費點：`Creature.MaxHp`（血量乘數）、`PlayerVitals` 的傷害計算（傷害乘數）
- `SaveManager.CreateServer` 多了 `Difficulty` 參數，`ServerSaveObject` 多了該欄位

---

## 3. BepInEx / Harmony 的三條硬規則（全部已驗證）

### 3.1 patch 類別上必須有 `[HarmonyPatch]`

`Harmony.PatchAll(Type)` 建立的 `PatchClassProcessor` 若在**型別上**找不到任何
Harmony 標註就直接略過整個類別——方法上的標註完全不會被掃到，
而且**不會報錯**。所有 patch 類別都要這樣寫：

```csharp
[HarmonyPatch]              // ← 這個空的是必要的
internal static class Patches { ... }
```

### 3.2 Mono 會 inline 一行的 auto-property，不要 patch 它們

```csharp
// ServerSettings
public static float HealthMultiplier { get; private set; } = 1f;
// Creature
public int MaxHp => (int)((float)this._maxHp * ServerSettings.HealthMultiplier);
```

對 `get_HealthMultiplier` 下 patch，在 `MaxHp` 這個呼叫端**很可能完全不生效**，
而且沒有錯誤、沒有 log。判準：

- **短的 getter/setter → 不要 patch，直接寫值**（反射呼叫私有 setter），
  並在遊戲設定它的地方之後補蓋一次。
- **夠大的方法 → patch 可靠**。已驗證安全的：
  `Item.TotalWorth`（五個乘法 + `AnimationCurve.Evaluate`）、
  `MoneyManager.RemoveMoney`、`CreatureManager.GetRandomItem`、
  `Radio.ApplyVolume`、`PlayerVitals.OnStartServer`。

### 3.3 不能新增 SyncVar 或 ServerRpc

FishNet 的同步欄位與 RPC 是**編譯期 IL weaving** 產生的，Harmony 補不上。
所以 mod 只能改「伺服器端算出來的結果」，不能建立新的同步狀態。
需要同步時走遊戲自己的方法（例：友傷用 `ServerSettings.ToggleFriendlyFire()`
而不是攔 getter，這樣其他客戶端才收得到正確的值）。

---

## 4. IMGUI 的結構一致性規則（已驗證，踩過 NRE）

Unity 每幀對 `OnGUI` 跑**多次**：先 `Layout` 算版面，再跑輸入事件，最後 `Repaint`。
若在後面那幾次裡改動**版面結構**（控件數量、群組巢狀、某塊要不要顯示），
就會和 `Layout` 算出的結構對不上，GUILayout 取到 null 直接 NRE
——**堆疊會指在當下那個繪製函式裡，很容易誤判成那個函式自己的 bug**。

規則：**會改變結構的狀態變更一律 `Defer()`，只在 `Layout` 事件套用。**

已實際踩到的結構變動：

| 動作 | 為什麼 |
|---|---|
| 開／關覆蓋層（選單、picker） | 整個視窗換成另一種內容 |
| 切換分頁 | 清單整份換掉 |
| 搜尋框過濾 | 項目數量改變 |
| 條件式顯示的提示文字 | 有值時不畫，數量會變 |
| 輸出區新增／清除訊息 | Label 數量改變 |
| **切換 bool 設定** | 若該 bool 控制某塊要不要顯示（例：ConfigMenu 的「顯示原始值」） |
| `PosOrVoid` 之類的模式切換 | 某模式會多出幾個輸入框 |

配套手法：

- 過濾／清單結果在 `Layout` 取快照，其餘事件沿用同一份。
- 文字框本身即時更新（純改值不影響結構），但**過濾用快照**。
- 模式狀態存獨立欄位，不要從參數字串反推——使用者把內容刪空時會在幀中途翻轉。
- **參數列要依寬度預算自動換行**，按鈕獨立一列。GUILayout 會把排在最後的元素
  擠出可視範圍（`/hitplayer` 三個參數就會讓「執行」按鈕整個消失）。

### 其他已驗證的 IMGUI 事實

- IMGUI **確實會畫在遊戲的 URP Overlay Canvas 之上**（有設 `GUI.depth = -1000`）。
- `Font.CreateDynamicFontFromOSFont("Microsoft JhengHei UI", 13)` 拿得到字型，中文正常。
- 開視窗時要 `PlayerCamera.ToggleMouse(true)`，但**關閉時不要自己鎖游標**——
  `ToggleMouse` 內部已處理「暫停中 / 在主選單 / 思考中必須保持解鎖」，
  再硬鎖一次會讓「從 ESC 選單開啟再關掉」變成游標消失。
- 擋輸入用 `Player.BlockInputs` 的 getter postfix（回傳 true），
  它被相機、持有、移動、船等各處消費，一個點就全擋住。

---

## 5. 遊戲端的守衛：為什麼有些指令「看起來沒用」（全部已驗證）

這些**全是遊戲自己的限制**，不是 mod 壞掉。

| 現象 | 原因 |
|---|---|
| `/setitemmultiplier` 對已有倍率的物品無效 | `Item.SetKillscoreMultiplier` 第二道守衛 `if (_killScoreMultiplier.Value != 1f) return;`——一個物品只能設一次 |
| 搶不走別人手上的物品 | `RpcLogic___SetItemHolder`：`if (syncedHolder && syncedHolder != A_2) { TargetReconcileRejectedItemPickup(...); return; }`。只能把**無主**物品塞進某人手裡 |
| `/tpitems` 當房主時完全沒反應 | `RigidbodySync.ServerSetPosRot` 整個方法體包在 `if (netCon != InstanceFinder.ClientManager.Connection)` 裡。那是**防回音**用的：正常流程（`RigidbodySync.cs:730`）送的就是自己的連線。你是房主時條件永遠為假 |
| `/hijackitemphysics` 看不出效果 | 只改 `_syncedSimulator`，本來就沒有視覺變化 |
| `/forceplacebet` 選綠沒開出綠 | `ServerStartBet(chosenColor)` 設的是 `_curBetColor`＝**你押的顏色**，不是開獎結果。另需 `HasPlacedBet`（桌上 `_totalWorth > 0`）且 `!IsBetting` |
| `/spoofroulette` 沒有實質效果 | `CasinoManager.UpdateGameObjects(pos, angle)` 只廣播輪盤物件擺放，**純視覺**，不影響結果或賠付 |
| `/steerboat` 沒反應 | `Boat.ServerSetInput` 開頭 `if (!IsServerInitialized \|\| !_driver.Value) return;`。且真駕駛每幀都送輸入（`Boat.cs:721`），單發下一幀就被蓋掉 |

### 對應的正確做法

- **搬物品**：不要送 RPC，用公開的 `RigidbodySync.StartSimulateLocal(pos, rot)`
  先接管模擬權再定位（房主／客戶端都有效）。
  已經是本機在模擬時它會**提早 return 而不搬動**，那種情況改用 `TeleportToPosRot()`；
  `pos` 剛好等於 `Vector3.zero` 時它也會略過定位。
- **操舵**：需要先有駕駛，且要持續每幀重送才蓋得過真駕駛。

---

## 6. 各系統的關鍵接點（已驗證）

### 釣魚
- **唯一入口**：`CreatureManager.GetRandomItem(Vector3 pos, List<ItemInfoWeight> weights)`。
  呼叫者只有兩處：`CreatureManager.cs:108`（`bait.Info.ItemWeights`）與
  `ExplosionManager.cs:49`。
- 用 prefix 換掉 `ref` 參數為**副本**，不要改 `BaitInfo.ItemWeights` 本身
  ——那是共用 ScriptableObject，改下去整個 session 都髒了。
- 保留原方法的邏輯（「場上已有 Boss 就退回預設魚」、權重跑完沒中的保險），
  不要自己重寫抽取。
- `ItemInfoWeight` 私有欄位：`fishable`（注意沒有底線）、`_weight`。
- **魚沒有稀有度欄位**——`Rarity` 只用在 `ItemSkin`。要判稀有只能用權重本身。
- 咬鉤時間：`Bait.cs:162` 讀 `Info.CatchTimeMinMax`。`BaitInfo._catchTimeMinMax` 是私有欄位，
  改它要做原值快照並在退出時還原。**不要** patch `Bait.RandomizedCatchTime` 的 setter（見 3.2）。

### 經濟
- **唯一售價算式**：`Item.TotalWorth`
  ＝ `_worth × 隨機重量 × 熟度曲線 × 賭注倍率 × 擊殺分數倍率`。
  `MoneyManager.SellItem` 直接用它加錢，UI 也用它顯示。
- **購買的唯一匯流點**：`MoneyManager.RemoveMoney`。
  `Server.cs` 裡所有購買 RPC（商店、口袋、魚餌、馬達、配件、子彈與銳利度升級、賭注）都走它。
  `[未驗證]` 商店 UI 顯示的價格散落在各 `Purchasable` 子類，沒有統一接點。
- 起始金錢：`MoneyManager.OnStartServer` 讀 `SaveManager.CurServerSave.Money`；
  `SaveManager.CreateServer` 最後才指派 `CurServerSave`，所以接在它 postfix。

### 玩家數值
- `PlayerVitals` 的 tick（飽食、回血、中毒、著火）**只在伺服器端跑**——
  `OnStartServer` 才把 `TickUpdate` 掛上 `TimeManager.OnTick`。房主裝就夠。
- 可調欄位（全私有 `[SerializeField]`，名稱已驗證）：
  `_loseFullnessTickInterval`(uint 300)、`_fullnessLostPerTickInterval`(uint 1)、
  `_loseHealthHungerTickInterval`(uint 150)、`_healthLostPerHungerTick`(int 5)、
  `_gainHealthTickInterval`(uint 100)、`_healthGainedPerTickInterval`(int 5)、
  `_poisonTickInterval`(uint 100)、`_poisonDamagePerTickInterval`(int 5)、
  `_fireTickInterval`(uint 50)、`_fireDamagePerTickInterval`(int 10)、
  `_playerDamageMultiplier`(float 0.25，玩家互打)、`_healthOnRes`(int 25)、
  `_fullnessOnRes`(int 10)、`_invulnerabilityAfterDamage`(float 0.25)。
- **做不到**：提高生命上限。`Regenerate()` 的上限是寫死的字面值 `100`，不是欄位。

### 收音機
- `Radio._channels`（`RadioChannel[]`）、`Radio._noiseSource`、`Radio._radioVol`(0.3)、
  `Radio._localFrequency`；`RadioChannel._channelSource`。全私有。
- `_localFrequency` **在所有情況下都追得上目前頻率**：本機持有時自己更新，
  別人持有時由 `OnFrequencyChange` 寫入。要重算音量用它就好，不必碰 SyncVar。
- 「沙沙聲」**是刻意的**，`ApplyVolume()` 最後一行：
  ```csharp
  _noiseSource.volume = (1f - 最接近頻道的準度) * 0.075f;
  // 準度 d = Mathf.Clamp01(1f - (Mathf.Abs(freq - ch.Frequency) - 0.5f))
  ```
  誤差 ±0.5 內完全無雜訊，±1.5 之後全雜訊——模擬 FM 空頻噪音。
- **Boss 登場時整個收音機靜音**（所有頻道與雜訊歸零後提早 return）。
- `ToggleMute(false, t)` 會做 `_channelSource.time = t % clip.length`，
  所以 **clip 不能是 null**，且自訂音檔**不能用串流載入**（seek 不可靠）。

### 物品 / 準心
- `ItemManager.Items` 是 `Dictionary<Transform, Item>`，以**每個碰撞體的 transform**
  為鍵建立（`ItemManager.cs:311` 的 `TryAdd(collider.transform, item)`），
  所以射線命中點可以直接查表。
- 重量（kg）＝ `Item._weight`（protected）× `Item.RandomizedWeight`
  ——照抄 `Creature.cs:335` 的 inspect 文字算法。
- 生命上限不要寫死 100，用 `Health / HealthPercent` 反推。
- `RigidbodySync` 公開可用：`SyncedSimulator`、`IsSimulatedLocal`、
  `StartSimulateLocal(pos, rot)`、`TeleportToPosRot(pos, rot, ...)`、
  `Freeze(bool frozen, bool permanent)`、`SetKinematic`、`ServerSetIsFloating`。

---

## 7. 選單 UI 注入（部分已驗證）

**已驗證的失敗原因**：

- `MainMenuManager._menuStuff` 是**3D 場景物件**（跟 `_menuCam`、`_boat` 同一組），
  主選單的 UI 完全不在它底下——UI 由 `CanvasManager.ToggleMenuUI()` 管理。
  在 `_menuStuff` 裡找 Button 永遠是零。
- `PauseManager._mainScreen` 是對的根（暫停選單的按鈕欄），但**用 `Button.onClick`
  持久監聽者比對方法名**（`MainMenuButton` / `BackOrCloseButton`）找不到東西。
  `[未驗證]` 那些按鈕實際綁的是什麼還沒查出來。

**已驗證的必要善後**：

- 複製品上**型別名含 `Localize` 的元件全部要銷毀**。留著的話，語系初始化或
  物件重新啟用時會用字典裡的原字串把設好的文字蓋回去。
- **整個換掉 `onClick`**（`button.onClick = new Button.ButtonClickedEvent()`）。
  prefab 綁死的持久監聽者無法逐條移除，不換掉的話按下去會連原本的動作一起觸發。
- `UIButton` 只負責 hover 音效與縮放特效，**真正的點擊是 `UnityEngine.UI.Button`**。
  複製整顆 GameObject 兩者都會跟著來。

**目前採用的策略**：不靠階層路徑也不靠方法名，改成把候選 Button 依 parent 分組，
挑出最像主要按鈕直欄的那一組。主選單改全場景掃描並排除 `PauseManager._pauseHolder`
底下的，另過濾 `CanvasGroup.alpha < 0.1 || !blocksRaycasts` 的隱藏畫面。
**按鈕確實會插進兩邊的選單（已實測）。**

### 挑到欄位還不夠，挑「哪一顆當模板」也必須是決定性的（已實測到症狀）

**症狀**：按鈕會出現，但**每次重開遊戲位置都不一樣**。

**原因**：主選單的候選來自 `FindObjectsByType(..., FindObjectsSortMode.None)`
——那個順序 Unity 文件明講是未定義的。舊版直接拿「候選清單裡的最後一顆」當模板，
再照它的 `GetSiblingIndex()` 插入，所以每次啟動其實是拿到**同一欄裡的不同按鈕**，
複製品的位置就跟著跳。分組用的 `Dictionary` 迭代順序同理，平手時也是看誰先進表。

**修法**（`[未驗證]`，等實測）：整條選法排成決定性的——

- 欄內先 `Sort` 依 `GetSiblingIndex()`，「最後一顆」才是畫面上最下面那顆
- 欄與欄之間用固定鍵比大小：
  `VerticalLayoutGroup > 其他 LayoutGroup > 沒有` → 欄內按鈕數 → 階層路徑 → parent 的 sibling index
- 插入位置由設定決定（最後一顆的上面／最下面／最上面），
  算 index 用**現有按鈕的實際 sibling index**，不是 0 / `childCount`
  （欄位裡常夾著標題、分隔線那類非按鈕物件）

通則：**任何 `FindObjectsByType` / `Dictionary` 迭代的結果拿來當定位依據之前，
一定要自己排一次**，否則就是這種「每次啟動不一樣」的 bug。

`HtF.ConfigMenu` 有「診斷：印出按鈕分組」設定可以印出實際結構（含 layout 分級）。

### 語系（已驗證）

`LocalizationManager.CurLanguage` 是 **public static 屬性**（private set），
語系還沒初始化時回 0，不會 NRE，可以直接讀來判斷遊戲目前的語言。
索引出自 `SetToSteamLanguage` 的 Steam 語言字串對照表：

`0` english、`1` swedish、`2` schinese、`3` tchinese、`4` french、`5` german、
`6` italian、`7` japanese、`8` koreana、`9` polish、`10` brazilian、`11` russian、
`12` latam、`13` spanish、`14` turkish、`15` ukrainian

另有 `LocalizationManager.IsAsianLanguage`（2/3/7/8 為 true，字型 fallback 用）。

### 七個 mod 的雙語架構（已實作，`[未驗證]` 尚未實測）

底層是 `mods/Shared/Loc.cs`，由 `Common.props` 的 `<Compile Include>` **編譯進每一個 mod**。
每個 DLL 各有一份自己的 `HtF.Shared.Loc` 靜態狀態，**執行期互不相依**，單獨安裝也能用。

| 要翻的東西 | 怎麼做 |
|---|---|
| 設定項 | `Loc.Bind(Config, section, key, 預設值, 英文名, 中文說明, 英文說明, range)` |
| section | `Loc.Section("倍率", "Multipliers")` |
| 列舉值 | `Loc.EnumValue(Force.不變, "不變", "Unchanged")` |
| 自己的 UI 文字 | `Loc.P("中文", "English")`，收在各 mod 的 `Localization.cs` |

**識別字絕對不要翻**：BepInEx 的 section / key 就是 `.cfg` 的欄位名，列舉成員名
（`Force.不變`、`Corner.左上`）就是寫進 `.cfg` 的值。翻譯它們＝換一次語言整份設定
被當成新項目重生，使用者的值全部掉回預設。同理，**送給遊戲的指令字串永遠不翻**
（`/spawn tuna`、`me`、`all`、`confirm` 是語法不是文案）。

**語言只有一個開關**：ConfigMenu 的「語言」設定。其他 mod 用 `Chainloader.PluginInfos`
去讀（節流 0.5 秒，比對 `Definition.Section == "介面" && Key == "語言"`，
取 `BoxedValue.ToString()`），沒裝 ConfigMenu 就退回遊戲語系。
因為跨組件的列舉是不同型別，這裡只能比字串，不能轉型。

**ConfigMenu 怎麼翻別人的設定**：它刻意不認識任何特定 mod，改用反射找
`asm.GetType("HtF.Shared.Loc")` 上的 `Term(string key, bool english, bool wantDesc)`，
綁成 `Func<string,bool,bool,string>` 快取起來——有就用，沒有就顯示原字串。
**這個方法的名稱與簽章就是那條約定的介面，不要改。**

`.cfg` 的註解（只在 Bind 當下寫一次）和 RadioMusic 的說明檔一律中英併列，
因為那些檔案沒辦法跟著語言重畫。說明字串裡的換行會被 BepInEx 寫成另一行 `##`（已驗證），
所以中英各自成段，讀起來剛好。

---

## 8. 目前的 mod 清單

全部在 `mods/`，各自獨立 DLL 與設定檔，互不相依。細節見 `mods/README.md`。

| 專案 | GUID | 誰要裝 | 主要掛鉤 |
|---|---|---|---|
| `HtF.HudNumbers` | `htf.hudnumbers` | 自己 | 無 patch，純唯讀 + OnGUI |
| `HtF.HostRules` | `htf.hostrules` | 房主 | 反射寫 `ServerSettings` 乘數 + `OnDifficultyChange` / `PlayerVitals.OnStartServer` postfix |
| `HtF.Economy` | `htf.economy` | 房主 | `Item.TotalWorth`、`MoneyManager.RemoveMoney`、`SaveManager.CreateServer` |
| `HtF.FishingEcology` | `htf.fishingecology` | 房主 | `CreatureManager.GetRandomItem` prefix/postfix + `BaitInfo._catchTimeMinMax` |
| `HtF.RadioMusic` | `htf.radiomusic` | 自己 | `Radio.OnStartClient` / `Radio.ApplyVolume` postfix |
| `HtF.ConfigMenu` | `htf.configmenu` | 自己 | `Player.BlockInputs` postfix；用 `Chainloader.PluginInfos` 列舉全部插件設定 |
| `HtF.DazedTools` | `htf.dazedtools` | 自己 | `DazedCommands.IsServerCommand` prefix 等四個 |

**GUID 改名時記得**：BepInEx 會依 GUID 生設定檔，改名後要把舊的 `.cfg` 一起搬，
並刪掉 profile 裡的舊 plugin 資料夾（否則兩份會同時載入，patch 打兩次）。

### BepInEx 設定 API（`HtF.ConfigMenu` 用到，已驗證）

- `Chainloader.PluginInfos` → `Dictionary<string, PluginInfo>`；
  `PluginInfo.Instance`（`BaseUnityPlugin`）、`.Metadata`（GUID/Name/Version）
- `BaseUnityPlugin.Config` → `ConfigFile`；`ConfigFile.GetConfigEntries()` → `ConfigEntryBase[]`
- `ConfigEntryBase`：`Definition`(Section/Key)、`Description`、`SettingType`、
  `DefaultValue`、`BoxedValue`(get/set)、`GetSerializedValue()`/`SetSerializedValue()`
- `ConfigDescription.AcceptableValues` → `AcceptableValueRange<T>`（`MinValue`/`MaxValue`，
  泛型，用反射讀屬性最省事）或 `AcceptableValueList<T>`（`AcceptableValues`）
- `ConfigFile.SaveOnConfigSet` **預設 true**——滑桿拖曳時每幀寫檔，
  要改成放開滑鼠才提交
- `KeyboardShortcut`：`MainKey`、`Modifiers`、`IsDown()`、`Serialize()`

---

## 9. 常見誤判提醒

1. **指令沒反應 ≠ mod 壞掉。** 先查第 5 節那張表，多半是遊戲自己的守衛。
2. **patch 沒生效但沒報錯 → 先懷疑 Mono inline**（第 3.2 節），不是 Harmony 沒掛上。
3. **NRE 指在某個 IMGUI 繪製函式裡 → 先懷疑結構不一致**（第 4 節），
   不是那個函式自己的問題。
4. **不要用階層路徑或按鈕文字定位 UI**，遊戲一改版就壞；也不要假設
   `XxxManager` 的欄位就是 UI（`_menuStuff` 是 3D 場景）。
5. **不要改共用的 ScriptableObject**（`BaitInfo`、`Fishable` 等）而不做快照還原，
   那會髒到整個 session 的其他存檔。
6. **版本字串不能拿來判斷 DLL 新舊**（來自 bundleVersion，見第 2 節）。
7. 反編譯源碼**無法整體編譯**（見 `AI_CONTEXT.md` 第 2 節），
   只能參照 `Assembly-CSharp.dll` 做 Harmony patch。

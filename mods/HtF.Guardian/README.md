# HtF.Guardian

房主端的 ServerRpc 驗證層。把遊戲丟掉的「這個封包是誰送的」接回來，用它替
56 條 `[ServerRpc(RequireOwnership = false)]` 逐一補上授權檢查與速率限制。

**只有房主要裝。** 所有 `RpcLogic___*` 都只在伺服器端跑，裝在純客戶端上完全不會執行到。

## 問題是什麼

遊戲是 host-authoritative（listen server）。`Server.cs` 有 56 個
`[ServerRpc(RequireOwnership = false)]`——`RequireOwnership = false` 關掉了 FishNet
自動的擁有者檢查，而遊戲**沒有補上自己的**。

FishNet 其實把答案送到門口了：它替每個 ServerRpc 的 reader 注入真實的發送端連線，

```csharp
private void RpcReader___HitCreature___215526726(PooledReader r, Channel channel, NetworkConnection conn)
{
    Creature creature = ...; Player player = ...; int damage = ...;
    if (!base.IsServerInitialized) return;
    this.RpcLogic___HitCreature___215526726(creature, player, damage, hitPoint, dir);   // ← conn 沒有傳下去
}
```

56 條 reader 裡，**只有 `SpawnPlayer` 把 `conn` 往下傳**（它拿去比對 Steam 大廳成員）。
其餘 55 條把它丟掉，於是伺服器只能相信客戶端在參數裡自填的
`Player` / `SteamID` / `cost` 來決定「誰在操作、對誰操作、花多少錢」。

結果就是：任何連進來的人都可以對任意玩家造成任意傷害、把任意玩家傳送出地圖、
清空任意玩家的背包、冒名發言、用 `cost = 0` 白拿東西、替全隊押注、強制結束遊戲。

## 做法

```
RpcReader___X(reader, channel, conn)
        │
        │  ① prefix：Sender.Begin(conn)          ← 把發送端存起來
        ▼
   （讀參數）
        │
        ▼
RpcLogic___X(a, b, c)
        │
        │  ② prefix：用 Sender.Current 驗證 a/b/c
        │     不合 → return false（不執行原方法）
        │     可修正 → 改寫 ref 參數後放行
        ▼
   （原本的邏輯）
        │
        │  ③ reader postfix：Sender.End()
```

| 檔案 | 作用 |
|---|---|
| `src/Plugin.cs` | BepInEx 進入點、設定項、熱鍵 |
| `src/Patcher.cs` | 找目標並掛 patch；啟動時印涵蓋率 |
| `src/Sender.cs` | 「現在這個 RPC 是誰送的」＋ 連線 ↔ Player ↔ SteamID |
| `src/Guards.cs` | 56 個守衛本體 |
| `src/Report.cs` | 記錄違規、處置（踢出／封鎖）、餵資料給面板 |
| `src/Limiter.cs` | 每連線每 RPC 的權杖桶 |
| `src/Prices.cs` | 從場上販賣點取真實售價 |
| `src/Speed.cs` | 移動速度檢查（預設關閉） |
| `src/Bans.cs` | 封鎖名單檔 |
| `src/Watcher.cs` | 連線事件：封鎖名單在這裡生效 |
| `src/Panel.cs` `src/Styles.cs` | F11 監控面板 |

### 目標一律用前綴找，不寫死方法名

weaver 產生的方法叫 `RpcLogic___HitCreature___215526726`，後面那串是簽章雜湊，
遊戲改一次參數就會變。所以用 `"RpcLogic___HitCreature___"` 當前綴搜，
真的整個不見了就在啟動時留一行警告——**不會默默失效**。

啟動 log 會印涵蓋率：

```
守衛 56 / 56 條 RPC，reader 56 條。
```

遊戲更新加了新的 ServerRpc 時，那個分母會變大，還會多一行「沒有守衛的：…」。

### 參數用位置注入 `__0` `__1`，不是名字

**這條是硬規則。** `RpcLogic___*` 的參數在 metadata 裡**沒有名字**
（反編譯看到的 `A_1`、`A_2` 是 dnSpy 對無名參數的填充），照名字綁一定失敗。
有趣的是同一個 weaver 產生的 reader **有**名字（`PooledReader0` / `channel` / `conn`）。

位置注入還有一個好處：遊戲改參數名不影響我們。要改寫參數就宣告成 `ref`。
本專案已用遊戲自己的 `0Harmony.dll`（HarmonyX 2.9.0）實測過
`__N` 綁定、`ref __N` 寫回、prefix 回傳 false 跳過原方法三件事都成立。

### reader 要濾掉 TargetRpc

`Server` 上有 57 個 reader：56 個 ServerRpc 的是
`(PooledReader, Channel, NetworkConnection)`，另外一個
`TargetReconcileRejectedItemPickup` 是 TargetRpc，**只有兩個參數**。
對它綁 `__2` 會在 patch 當下就丟例外，所以要先看參數數量與型別。

### 守衛不能丟例外

FishNet 把「RPC 解析／執行期間丟出任何例外」當成惡意封包，**直接踢掉發送者**
（`ServerManager.Kick(KickReason.MalformedData)`，FishNet.Runtime 的
`Managing/Server/ServerManager.cs:1111-1119`）。

所以守衛裡只有 null 檢查、比大小、比連線；需要反射或掃場景的部分都關在
自己的 try/catch 裡。**守衛自己的 bug 不該變成踢人。**

## 檢查了什麼

### 操作者身分（`檢查操作者身分`）

RPC 參數裡的 `Player`（或物品的持有者）必須就是送出封包的那條連線。

這一條擋掉絕大多數的搗亂：強制移動別人、把別人傳送出地圖、清空別人的背包、
替別人切換道具欄、幫別人的槍花全隊的錢買配件、冒名發言、把別人踹下駕駛座。

驗證方式是 `player.Owner` 對上 reader 給的 `conn`——`Player` 是用
`base.Spawn(player.gameObject, conn, ...)` 生成的，擁有者就是當初送 `SpawnPlayer` 的人。

### 價格（`檢查購買價格`）

魚餌、船馬達、船雷達這三條把價格當參數讓客戶端自己填。**正解不是硬寫價格表**，
而是去問場上那個販賣點：價格是 `Purchasable._customCost`，遊戲自己送的就是這個值。

允許的是**一組**值而不是單一值——同一種魚餌可能同時有付費攤位和免費攤位
（`_isFree` 會讓 `_customCost` 變 0），教學區的餌就是免費的。
價格對不上時**不擋，改成正確價格再放行**，並記一筆。

只掃啟用中的物件：`BaitPurchasable._customCost` 是在 `Awake` 裡算出來的，
沒醒過的物件上那個欄位還是 0，收進來會變成一筆假的「這種餌免費」。

`BuyItem` 的 `isFree` 布林同理——傳 `true` 就整個跳過扣款，守衛把它改回 `false`。

### 數值（`檢查數值範圍`）

| 項目 | 規則 |
|---|---|
| 對生物的傷害 | `0 … 生物傷害上限`。**負值一律擋**——`ServerChangeHp` 是 `_hp.Value -= damage`，負傷害等於替 Boss 回血 |
| 對玩家的傷害（有攻擊者） | `0 … 玩家傷害上限` |
| 對玩家的傷害（無攻擊者） | `0 … 無來源傷害上限` ＋ 獨立的速率桶 |
| 分數倍率 | `0 … 分數倍率上限` |
| 復活進度 | 夾到 `0…1` |
| 單次彈丸數 | `≤ 單次彈丸數上限` |
| 聊天字數 | 超過就截斷 |
| 座標／旋轉／頻率 | 擋掉 NaN 與無限大 |

傷害 0 是放行的：伺服器端本來就是無動作（`if (A_2 != 0)`），擋它只會製造假違規。

### 索引（`檢查索引範圍`）

這幾個不是作弊而是**當機**：遊戲的守衛漏了下界，而任何例外都會讓發送者被踢。

- `UnlockPocket`：`_extraSlotCosts[index - 1]`，`byte 0` 在減法時提升成 int 變 **−1**（不是 255）。遊戲只擋了 `> 5`。
- `BuyBait`：`_ownedBaits[index - 1]`，同一個陷阱。
- `TakeItemFromNpc`：守衛是 `if (A_2 != 255 && !NpcIsHoldingItem(A_2)) return;`——**id 為 255 時整個守衛被跳過**，接著走到字典索引器 `_idToNpc[255]`。
- `PlaceBet` / `UpdateRoulette`：`CasinoManager.Instance` 是純靜態欄位，離開賭場島後是「已銷毀但非 null」的 Unity 物件。

### 速率（`速率限制`）

每條連線的每個 RPC 各一個權杖桶。用桶而不是固定視窗計數，是因為正常玩本來就會
爆發性地送封包（一梭子彈、撿一整排東西），固定視窗會把那些切掉。

上限值寫在各守衛裡（位置更新 120/s、物品位置 400/s、購買 10/s、聊天 3/s…），
`速率上限倍率` 一次調整全部。

### 連線層

- **一條連線只能有一個 Player**：原本沒有這個檢查，重複送 `SpawnPlayer` 就會多生一隻。
- **封鎖名單**：`Kick` 只是把連線斷掉，對方可以立刻再連。名單在連線建立時比對，
  用的是 `NetworkConnection.GetAddress()` 給的 Steam ID（傳輸層來源，偽造不了）。

## 擋不住的（誠實列出）

- **沒有攻擊者的玩家傷害**。`HitPlayer` 的友傷檢查是
  `if (A_6 && !UseFriendlyFire) return;`——攻擊者傳 `null` 就整個跳過。
  而 `null` 攻擊者本來就是合法的（溺水、生物撞擊、Boss），
  遊戲又允許**任何**客戶端代生物送出（`AttackingFish.DamageOnCollision`
  只看 `_rigSync.IsSimulatedLocal`）。沒有身分可以驗，只能夾上限加限速。
- **免費攤位存在的魚餌**可以被無限白拿——因為 `cost = 0` 對那種餌是合法值。
- **小幅度的加速／飛行**。移動速度檢查預設關著，理由見下。
- **錢是全隊共用的單一數字**（`MoneyManager.Money`），不是 per-player。
  身分檢查能擋「幫別人買」，擋不掉「自己把全隊的錢花光」。那需要改遊戲的經濟模型。

## 移動速度檢查為什麼預設關著

位置更新走 unreliable 通道，掉包、亂序、換島傳送、上船都會讓
「兩次更新之間的距離 ÷ 時間」暴衝。所以那段邏輯保守到近乎溫和：
超過 1 秒沒更新就只重設基準不判定；一次超速不算，要**連續 5 次**才擋；
擋下來只是丟掉那個位置封包，不會踢人。

即使這樣它還是會誤判，而且擋不住小幅加速——成本效益本來就不好，
留給「明知道有人在飛」的時候開。

## 面板（預設 F11）

三個分頁：

- **連線** —— 每個人的違規次數、Steam ID、最後一次被擋的原因，加上踢出／封鎖按鈕。
- **事件** —— 最近 80 次被擋下來的操作（誰、哪條 RPC、什麼原因、多久以前）。
- **封鎖** —— 名單內容，可以逐筆解除。

踢出與封鎖是**兩段式**的：第一下把按鈕變成「確定？」，第二下才送出，三秒沒動作自動解除。

面板全部用固定 Rect 的 `GUI.*` 畫，**沒有用 GUILayout**。
本專案其他 IMGUI mod 有一條「會改變版面結構的狀態變更要延到 Layout 事件」的規則，
那是 GUILayout 專屬的問題——它在 Layout 幀算好控件樹，之後的事件照那棵樹取值，
對不上就 NRE。固定 Rect 沒有那棵樹，所以切分頁、加一列事件、按鈕變成「確定？」
都可以當場生效。代價是要自己算座標。

連線清單的快照在 `Update` 算好、`OnGUI` 只讀：`OnGUI` 一幀會跑好幾次，
而 `GetAddress()` 會問到傳輸層，不是可以一幀呼叫十幾次的東西。

## 設定（`BepInEx/config/htf.guardian.cfg`）

| 分區 | 項目 |
|---|---|
| 一般 | 啟用、也檢查房主自己 |
| 驗證 | 檢查操作者身分／購買價格／數值範圍／索引範圍、速率限制、速率上限倍率、結束遊戲限房主、移動速度檢查、最高移動速度 |
| 上限 | 玩家傷害、無來源傷害、生物傷害、分數倍率、單次彈丸數、聊天字數 |
| 處置 | 違規上限、超過時（只記錄／踢出／踢出並封鎖）、通報冷卻秒數、在聊天視窗提示 |
| 介面 | 面板按鍵、縮放、字型 |

**處置預設是「只記錄」。** 先看幾場面板上的數字再決定要不要自動踢人——
延遲本來就會製造零星的假違規，而踢錯人比漏抓一次難收拾。

**「也檢查房主自己」預設關閉**：房主的連線就是伺服器本身，遊戲有好幾處是在伺服器端
代所有人送 RPC 的（`ExplosionManager.ServerExplode` 的爆炸傷害、引信到期的炸藥、
Boss 攻擊），用一般規則檢查它們一定誤判。

封鎖名單在 `BepInEx/config/HtF.Guardian/banned.txt`，一行一個 Steam ID，
`#` 之後是註解。遊戲執行中手動改檔案的話，要在面板上按「重新載入名單」。

## 和 HtF.DazedTools 一起用

`HtF.DazedTools` 送的就是這些 RPC。**你當房主時它們不衝突**——房主預設豁免，
所以指令照常有效。你在別人的房間裡用 DazedTools，而對方裝了 Guardian，
那些指令就會被擋掉，這正是預期行為。

## 建置

```bash
dotnet build mods/HtF.Guardian/HtF.Guardian.csproj
```

路徑可用 `-p:GameManaged="..."` / `-p:ProfileDir="..."` 覆寫，預設寫在 `../Common.props`。

## 尚未在遊戲內驗證

程式碼層面已經做過的驗證：56 條 RPC 的參數位置與型別、守衛的回傳型別，
都用 `MetadataLoadContext` 對**遊戲實際載入的那份 `Assembly-CSharp.dll`** 逐一比對過
（63 個位置綁定，0 個不符）；`__N` 位置注入與 `ref` 寫回也用遊戲自己的
`0Harmony.dll` 實跑驗證過。

`[未驗證]` 但仍需要實際連線測試的：多人房間裡的誤判率、各個速率上限的實際餘裕、
被擋下來時客戶端的表現（本機預測已經演出效果、伺服器沒接受，會看到位置或血量彈回）。

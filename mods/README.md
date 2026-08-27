# How to Fish — 七個獨立 mod

每個都是獨立的 DLL、獨立的設定檔，可以單獨安裝，彼此之間沒有相依。
共用的只有建置設定（`Common.props`）和雙語底層（`Shared/Loc.cs`），
兩者都是編譯期的東西——`Shared` 是直接編進每個 DLL 的，不是執行期相依。

| 專案 | Thunderstore | GUID | 誰要裝 | 做什麼 |
|---|---|---|---|---|
| `HtF.HudNumbers` | `HtF_HudNumbers` | `htf.hudnumbers` | 只有你自己 | 血量／飽食／物品數值化 |
| `HtF.AmmoCounter` | `HtF_AmmoCounter` | `htf.ammocounter` | 只有你自己 | 手上槍械的剩餘子彈 |
| `HtF.Guardian` | `HtF_Guardian` | `htf.guardian` | 只有房主 | ServerRpc 驗證層、速率限制、踢出／封鎖 |
| `HtF.HostRules` | `HtF_HostRules` | `htf.hostrules` | 只有房主 | 無段式難度、規則開關、玩家數值、死亡不掉落、抽魚權重、保底、咬鉤時間 |
| `HtF.RadioMusic` | `HtF_RadioMusic` | `htf.radiomusic` | 只有你自己（同步播放則全員） | 收音機自訂音樂、自動接下一首、一起聽、雜訊與音量 |
| `HtF.ConfigMenu` | `HtF_ConfigMenu` | `htf.configmenu` | 只有你自己 | 遊戲內設定管理頁面（通用） |
| `HtF.DazedTools` | `HtF_DazedTools` | `htf.dazedtools` | 只有你自己 | ServerRPC 指令工具（見該資料夾的 README） |

`HtF.Guardian` 和 `HtF.DazedTools` 是同一件事的兩面：一個送這些 RPC，一個擋這些 RPC。
你當房主時兩個一起裝不會打架（房主預設豁免），見 `HtF.Guardian/README.md`。

```bash
dotnet build HtF.HudNumbers/HtF.HudNumbers.csproj
```

每個專案建置後會自動部署到 r2modman 的 profile。路徑可用
`-p:GameManaged="..."` / `-p:ProfileDir="..."` 覆寫。

## 每個資料夾裡還有什麼

除了程式碼，每個 mod 資料夾都有一組 Thunderstore 套件用的檔案——它們就是套件
zip 根目錄要放的東西：

| 檔案 | 用途 |
|---|---|
| `manifest.json` | 套件名（點換成底線）、版本、說明、相依 |
| `README.md` | **英文**。Thunderstore 頁面顯示的就是它 |
| `README_ZH.md` | 同一份的繁體中文版 |
| `CHANGELOG.md` | 版本更新記錄 |
| `icon.png` | 256×256 的 PNG，由 `.github/tools/make_icons.py` 產生 |

版本號的單一來源是 `.csproj` 的 `<Version>`：`Common.props` 的 `GenerateModInfo`
把它產生成 `ModInfo.Version` 供 `[BepInPlugin]` 引用，`manifest.json` 裡那一份則由
`.github/tools/check_repo.py` 對帳。細節見根目錄 README 的〈發布到 Thunderstore〉。

**改過這個資料夾裡的任何 `.cs` 就要重新打包**——`dist/` 裡的 zip 是某一次建置的快照。
而且如果那個版本號已經上架過，得先把 `<Version>` 往上加一版再打包，
Thunderstore 不收重覆的版本號。步驟見根 README 的〈改過原始碼就要重新打包〉。

---

## 中英雙語（全部 mod）

七個 mod 的介面都有中英兩份文字。底層是 `mods/Shared/Loc.cs`，由 `Common.props`
**編譯進每一個 mod**——跟建置設定一樣是編譯期共用，每個 DLL 裡都有自己的一份，
執行期彼此不相依，單獨安裝照樣能用。

**語言只有一個開關**：`HtF.ConfigMenu` 的「語言」設定（`自動 / 中文 / 英文`）。
其他 mod 透過 BepInEx 的 `Chainloader` 讀它（每 0.5 秒一次），沒裝 ConfigMenu 就
自己跟著遊戲語系走——判斷依據是 `LocalizationManager.CurLanguage`
（出自 `SetToSteamLanguage` 的對照表，2 = 簡體、3 = 繁體，其餘一律當英文）。
每幀重判一次，換語言即時生效，不用重開遊戲。

### 一條要守住的界線

**BepInEx 的 section / key 是識別字，不是顯示字。** 它們就是 `.cfg` 裡的欄位名，
翻譯它們＝換一次語言就把整份設定當成新項目重生，使用者調好的值全部掉回預設。
列舉成員名（`Force.不變`、`Corner.左上`）同理，那是寫進 `.cfg` 的值。

所以識別字**永遠維持原本那串中文**，只有畫出來的那一刻才查表換語言：

```csharp
// 綁設定 + 登記英文名稱與中英說明，一個設定一行，不會兩邊對不上
SellMultiplier = Loc.Bind(Config, "倍率", "賣價倍率", 1.0f, "Sell Price Multiplier",
    "所有物品的售價（Item.TotalWorth）。小於 1 = 硬核經濟。",
    "Sell price of every item (Item.TotalWorth). Below 1 = hardcore economy.",
    new AcceptableValueRange<float>(0f, 100f));

Loc.Section("倍率", "Multipliers");                       // section 名稱
Loc.EnumValue(Force.不變, "不變", "Unchanged");            // 列舉值顯示文字
```

自己的 UI 文字則用 `Loc.P("中文", "English")`，各 mod 收在自己的 `Localization.cs`
（DazedTools 的指令說明在 `CommandRegistry.cs`，中英就寫在同一行上）。

### ConfigMenu 怎麼翻別人的設定

`HtF.ConfigMenu` 刻意不認識任何特定 mod。它是用反射找每個插件組件裡的
`HtF.Shared.Loc.Term(key, english, wantDesc)`——**有這個入口就用，沒有就顯示原字串**。
所以新 mod 只要照樣編進 `Shared/Loc.cs`，設定頁面就自動有雙語，ConfigMenu 一行都不用改；
別人寫的 mod 沒有這個入口，也只是照常顯示它自己的字串。

`Loc.Term` 的名稱與簽章是這條約定的介面，不要改。

### 檔案沒辦法跟著語言重畫的地方

`.cfg` 的註解只在 `Config.Bind` 當下寫一次，`HtF.RadioMusic` 的
「把音樂放這裡.txt」也是建資料夾時寫一次。這兩處一律**中英併列**。

---

## 為什麼有些地方不 patch getter

**這是這幾個 mod 最重要的設計決定，也是最容易踩的坑。**

Mono 會把一行的 auto-property inline 進呼叫端。例如：

```csharp
// ServerSettings
public static float HealthMultiplier { get; private set; } = 1f;

// Creature
public int MaxHp => (int)((float)this._maxHp * ServerSettings.HealthMultiplier);
```

對 `get_HealthMultiplier` 下 Harmony patch，在 `MaxHp` 這個呼叫端**很可能完全不生效**
——而且不會報錯、不會有 log，只是沒作用。這種 bug 極難查。

所以規則是：

- **短的 getter／setter → 不要 patch，直接寫值。**
  `HtF.HostRules` 用反射呼叫私有 setter 寫入乘數，並在遊戲設定它們的地方
  （`ServerSettings.OnDifficultyChange`）之後補蓋一次。
  `HtF.HostRules` 的咬鉤時間同理，改的是 `BaitInfo._catchTimeMinMax` 而不是
  `Bait.RandomizedCatchTime` 的 setter。
- **夠大的方法 → patch 沒問題。**
  `Item.TotalWorth`（五個乘法加一次 `AnimationCurve.Evaluate`）、
  `MoneyManager.RemoveMoney`、`CreatureManager.GetRandomItem` 都遠超過 Mono 的
  inline 門檻，patch 是可靠的。

另一條共用規則：**Harmony 無法新增 SyncVar 或 ServerRpc**（FishNet 靠編譯期 IL weaving），
所以這些 mod 一律只改「伺服器端算出來的結果」，不建立新的同步狀態。
需要同步的地方就走遊戲自己的方法——例如友傷是呼叫 `ServerSettings.ToggleFriendlyFire()`，
而不是攔截 getter，這樣其他客戶端才會收到正確的值。

---

## HtF.HudNumbers — HUD 數值化

**純客戶端、純唯讀，零 Harmony patch，也不送任何封包。** 在別人的房間用不影響任何人。

顯示：生命（實際值／上限／百分比）、飽食、中毒與著火（只在大於 0 時出現）、金錢、
手上物品、準心指向的物品或生物。物品資訊含名稱、售價（含基礎價）、重量（kg）、
熟度、分數倍率；生物另外顯示 HP／MaxHP 與 `[BOSS]` `[DRIP]` `[瀕危]` 標記。

準心判定是自己從攝影機打一條射線，再用 `ItemManager.Items` 查表——
那張表是以**每個碰撞體的 transform** 為鍵建的，所以命中點可以直接查到 Item。

生命上限不寫死 100，是用 `Health / HealthPercent` 反推的，遊戲改動時才不會顯示錯的分母。
重量的算法照抄 `Creature` 的 inspect 文字：`Item._weight × RandomizedWeight`。

預設 **F6** 開關，設定裡可改位置（四角）、縮放、字型、顯示哪幾塊、射線距離。

## HtF.AmmoCounter — 剩餘子彈顯示

**純客戶端、純唯讀，零 Harmony patch，也不送任何封包。** 跟 `HtF.HudNumbers` 同一條路子。

讀 `Weapon.Ammo`（彈匣內）和 `Weapon.Attachments.AmmoPerMag`（彈匣容量，會跟著
擴充彈匣配件變）。**這遊戲沒有備用彈藥的概念**——`Weapon.TryRefillAmmo` 是直接把彈匣填滿，
所以只有兩個數字，沒有第三個。

兩個都是公開讀取器，我們只讀不寫，所以**不受〈為什麼有些地方不 patch getter〉那條規則影響**
——那條講的是 patch 一行的 auto-property 會被 inline 掉，正常呼叫沒有這個問題。
唯一用到反射的是 `Weapon._isReloading`（裝填提示），遊戲沒有公開的查詢；
拿不到就只是不顯示提示字，其餘照常。

樣式可選 數字 / 圓點 / 兩者，位置預設在準心下方（也可以貼四角）。
彈匣容量超過 40 發時圓點列會自動退回只畫數字——一排點會橫跨整個畫面。
低於門檻轉警示色、空彈閃爍，門檻與閃爍都可以關。

文字**先偏移畫一次黑影再畫本體**：遊戲畫面是海和天空，淺色背景下純白字幾乎看不見，
而 `GUIStyle` 沒有描邊。

預設 **F10** 開關。瞄準（ADS）時可以設成自動隱藏，狙擊鏡才不會被擋。

## HtF.Guardian — 反外掛驗證層（房主端）

**只有房主要裝**，裝在純客戶端上不會執行到任何東西。完整說明在
`HtF.Guardian/README.md`，這裡只講設計上最關鍵的那一點。

遊戲的 `Server.cs` 有 56 個 `[ServerRpc(RequireOwnership = false)]`。
`RequireOwnership = false` 關掉了 FishNet 自動的擁有者檢查，而遊戲**沒有補上自己的**——
除了 `SpawnPlayer` 以外，沒有一個 `RpcLogic___*` 用到發送端連線，
於是伺服器只能相信客戶端在參數裡自填的 `Player` / `SteamID` / `cost`。

**FishNet 其實把答案送到門口了**：它給每個 `RpcReader___*` 注入真實的發送端
`NetworkConnection`（reader 的第三個參數），只是遊戲的 reader 把它丟掉。
這個 mod 就是在 reader 的 prefix 把它接起來，再在 `RpcLogic___*` 的 prefix 拿它驗證。

三件和其他 mod 不一樣、值得記住的事：

- **參數用位置注入 `__0` `__1`，不能用名字。** weaver 產生的 `RpcLogic___*` 參數在
  metadata 裡沒有名字（反編譯看到的 `A_1` 是 dnSpy 對無名參數的填充）。
  同一個 weaver 產生的 reader 反而**有**名字（`conn`）。
- **patch 目標用前綴找**（`"RpcLogic___HitCreature___"`），因為方法名尾巴是簽章雜湊，
  遊戲改參數就會變。找不到就在啟動時警告，不會默默失效。
- **守衛不能丟例外。** FishNet 把「RPC 執行期間丟例外」當成惡意封包直接踢掉發送者
  （`ServerManager.cs:1111-1119` 的 `Kick(KickReason.MalformedData)`），
  守衛自己的 bug 不該變成踢人。
- **「誰有資格」要照抄那條 RPC 自己的送出點閘門**，不能一律用持有者。
  `GrillItemInLava` 的閘門是 `RigidbodySync.IsSimulatedLocal`（丟進岩漿的東西沒人拿著），
  `SendBoatInput` 是駕駛，`SetItemMultiplier` 根本沒有操作者可驗（改成驗目標是不是死掉的生物）。

檢查分五類（各自可關）：操作者身分、購買價格、數值範圍、索引範圍、速率限制。
另外有封鎖名單和一個 **F11** 的監控面板。**處置預設是「只記錄」**，
先看幾場面板上的數字再決定要不要自動踢人。

它和 `HtF.DazedTools` 是同一件事的兩面。你當房主時兩個一起裝不會打架——房主預設豁免。

## HtF.HostRules — 房主規則擴充

把遊戲的三段式難度換成無段式，另外開放一批原本寫死的玩家數值。

- **難度乘數**：生物血量倍率、玩家受傷倍率。
  遊戲原本只有 簡單 0.75/0.5、普通 1/1、困難 1.25/1.25 三檔。
  關掉覆寫會還原成該難度的原值。
- **規則開關**：友軍傷害、一擊必殺，各自可設 不變／強制開啟／強制關閉。
  走遊戲自己的 toggle 方法，所以會正常同步給其他玩家。
- **玩家數值**（倍率作用在 prefab 原始值上，不會反覆疊加）：
  飢餓速度、飢餓扣血、回血速度、回血量、中毒傷害、著火傷害、
  玩家對玩家傷害（遊戲預設 0.25）、復活後生命／飽食、受傷後無敵秒數。

這些數值驅動的 tick 全部只在伺服器端跑（`PlayerVitals.OnStartServer` 才把
`TickUpdate` 掛上 `TimeManager.OnTick`），所以房主裝就夠。
改設定會立刻套用到場上每一位玩家，不用重開房間。

**做不到的**：提高生命上限。`Regenerate()` 裡的回血上限是寫死的字面值 `100`，
不是可調欄位，所以只改起始生命會得到一個半殘的結果——寧可不做。

### 死亡與暈倒時物品的去向（1.2.0）

遊戲掉東西走的是**兩條互不相干的路**，這是這個功能唯一需要先搞懂的事：

| 事件 | 路徑 | 掉什麼 |
|---|---|---|
| 暈倒（變成可被救起的 `DeadPlayer`） | `PlayerVitals` → `PlayerDying.ServerDie` | 只有**手上那一件** |
| 死亡（放棄、重生） | `Server.RpcLogic___RespawnPlayer` → `PlayerInventory.ServerDropAll` | 手上的**加整個背包** |

而且 `RespawnPlayer` 在**全員陣亡**時會對每一位玩家各跑一次 `ServerDropAll`，
不是只有按下放棄的那個人。兩條都只在伺服器端跑，所以房主裝就夠。

**「死亡不掉落背包」不是把 `ServerDropAll` 關掉。** 同一個方法也是 `DropAllItems`
這條 ServerRpc 的落點（`HtF.DazedTools` 的「掉光所有物品」走那裡），整個關掉會把一個
刻意送出的指令一起弄壞。改成用呼叫堆疊上的旗標分辨：`RespawnPlayer` 的 prefix 立起、
postfix 放下，`ServerDropAll` 的 prefix 只擋旗標立著的那一次。和 `HtF.Guardian` 的
`Sender.Begin/End` 是同一招。

**「暈倒不掉手上的東西」是收進背包，不是留在手上**——留在手上做不到。倒下的人**自己的
客戶端**會跑 `PlayerDying.LocalDie`，裡面無條件對 `HeldItem` 呼叫 `Drop(false, ...)`，
那是純本機的表現（`calledFromLocal = false` 那條路不送 RPC），房主端擋不到別人機器上的
那一行。

但 `LocalDie` 的第一件事是 `Inventory.ApplySlot(-1)`，而 `ApplySlot` 對**已經在背包裡**的
手持物走的是收納那條路（`Hands.DropItem` + `Holding.SetHeldItem(null)`），走完 `HeldItem`
就是 null，後面那個 `Drop` 根本不會執行。所以只要伺服器端先把它塞進背包，客戶端自己就
不會演出掉落。

⚠ **光在 prefix 塞進背包沒有用**，原方法會自己撤銷掉：`ServerDie` 最後那段的
`SetSyncedHolder(null, false)` 裡有一條「舊持有者的背包有這件就先 `RemoveItem`」的分支
（`Item.cs`）。所以還要 patch `Item.SetSyncedHolder`，在 `ServerDie` 的堆疊裡、而且是
「把持有者清成 null」的那一種呼叫才擋——範圍收得越窄越好，其餘交接／撿拾／丟棄都照常。

`[未驗證]` `LocalDie` 是客戶端用本機血量預測觸發的，可能比背包的同步先到，
那一瞬間會看到東西掉出來、同步追上才回到背包。實際上有多明顯要進遊戲才知道。

### 釣魚生態（1.1.0 從 HtF.Economy 併進來）

**為什麼併過來**：抽魚權重、保底、咬鉤時間從來就不屬於「經濟」——它們是**房主替整個
房間定的規則**，生效條件跟上面那些難度乘數一模一樣（唯一的讀取點只在伺服器端跑）。
放在另一個 mod 只是讓房主要開兩個設定頁面調同一件事。
`HtF.Economy` 的金錢那半（賣價／花費／起始金錢）連同它一起**移除**，不是搬家。

抽魚只有一個入口：`CreatureManager.GetRandomItem(pos, weights)`，
魚餌把 `BaitInfo.ItemWeights` 傳進去做加權隨機。

在進入該方法前，把 `ref` 參數換成調整過的**副本**——
**不會改到 `BaitInfo` 資產本身**，那是共用的 ScriptableObject，改下去整個 session 都會髒掉。
用 prefix 換表而不是自己重寫抽取邏輯也是刻意的：原方法裡有「場上已有 Boss 就退回
預設魚」這條規則和權重跑完沒中的保險，照抄一遍只會多出對不上的風險。

- **稀有度**：遊戲沒有魚的稀有度欄位（`Rarity` 只用在外觀），所以用權重本身判定——
  權重 ≤ 該表最大權重 × 門檻（預設 0.25）的就算稀有。可分別調稀有／常見／Boss 倍率。
- **個別倍率**：`tuna=5, giantpiranha=0.2` 這種格式，名稱用去空格全小寫（跟 `/spawn` 一樣），
  會覆蓋上面的分類倍率。
- **保底**：連續 N 次抽到非稀有後，下一次只從稀有項抽。計數全房共用，
  而且是 process 級的——換存檔、重開房間都不會歸零，要關掉遊戲才會。
- **咬鉤時間倍率**：改 `BaitInfo._catchTimeMinMax` 資產。唯一讀 `Bait.RandomizedCatchTime`
  的地方是 `CreatureManager.FindFishForBait`（`CreatureManager.cs:111`），只從 `TickUpdate`
  進得去，而 `TickUpdate` 只在 `OnStartServer` 掛上 `TimeManager.OnPostTick`。
  有做原值快照，倍率一律從快照算，**關掉「啟用釣魚生態」或退出時還原**。
  它走的是資產寫入而不是 patch，所以那個開關要在 `BaitTuner` 裡自己判——
  漏掉的話關掉開關只會停掉權重，咬鉤時間的倍率會留在資產上。

`除錯 / 記錄每次抽取` 打開後會把抽到什麼寫進 log，調倍率時很有用。
`釣魚 / 啟用釣魚生態` 關掉就完全走原本的權重，不影響上面的難度與玩家數值。

### 升級時要做的一件事

**把 `BepInEx/plugins/HtF.Economy`（和更早的 `HtF.FishingEcology`）整個資料夾刪掉。**
三者都用同樣的方式 patch `CreatureManager.GetRandomItem`——各自把 `ref weights` 換成
自己算的副本——兩份同時載入時倍率變成**疊乘**，而且不會有任何錯誤訊息。
這是 GUID 搬家最容易踩的坑，所以 mod 自己會在第一個 `Update` 檢查
`Chainloader.PluginInfos` 並在 log 大聲說一次。

（在 `Update` 而不是 `Awake` 檢查，是因為 BepInEx 邊載入邊填 `PluginInfos`，
在 `Awake` 當下對方可能還沒進去。）

設定不會沿用：GUID 換了，BepInEx 會另外開一份 `htf.hostrules.cfg`。
設定名稱維持原樣，重填很快。


## HtF.RadioMusic — 收音機自訂音樂

**純客戶端。** 頻率是 SyncVar 會同步（別人看得到你在轉台），但音檔是本地的，
所以只有你自己聽得到自訂音樂，不影響任何人，也不需要別人裝。
想跟別人一起聽，見下面〈同步播放〉。

把 `.ogg` / `.wav` / `.mp3` 丟進 `BepInEx/config/HtF.RadioMusic/`（第一次執行會自動建好，
裡面有說明檔）。直接放 = 依序輪流分給各頻道；放進子資料夾 `1\` `2\` = 指定給該頻道。
**F8** 重新掃描資料夾。

### 一首歌一個頻率（頻道數，預設自動）

上面那個「輪流平均分」是**在既有的頻道上**分。想要「轉到 91.0 就是這首、
轉到 94.0 就是那首」，就得真的把頻道加出來——`頻道 / 頻道數` **預設就是 0（自動）**：
一首歌一個頻率，平均攤在 88–108 上。沒有放自訂音樂的話完全不動遊戲原本的頻道。

做得到是因為 **`Radio._channels` 沒有任何地方寫死索引或數量**（查證過：`Radio.cs`
只有四處碰它——`ToggleChannels`、`ApplyVolume` 兩處迴圈、欄位宣告本身，全是逐一走訪），
所以換成更長的陣列是安全的。

三件實作上必須小心的事：

- **新頻道的 `AudioSource` 一定要有 clip。** `RadioChannel.ToggleMute(false, t)` 第一行是
  `_channelSource.time = t % _channelSource.clip.length;`——clip 是 null 就 NRE，
  而 `Radio.OnStartClient` 和 `OnEnable` 都會呼叫 `ToggleChannels(true)` 把每個頻道解除靜音。
  所以新的 source 是**複製現有那個 GameObject** 來的：連 clip、3D 衰減、mixer group
  一起帶過來，位置也對得上（收音機是位置音源，差一點就會聽起來在別的地方）。
- **但不能無條件複製那個 GameObject。** 頻道的 AudioSource 掛在哪裡是 prefab 決定的，
  反編譯看不到——如果它就掛在收音機自己那個物件上（同一個 GameObject 掛好幾個
  AudioSource 是合法的），複製它等於**複製整台收音機**。所以先確認那個物件的子樹裡
  沒有 `Radio`：沒有就直接複製（設定一個都不漏，含自訂衰減曲線），
  有就退成「開一個空子物件、`AddComponent`、逐項抄設定」，並留一行 log 說明走了哪條路。
- **台數一多就必須把衰減曲線收窄**，否則一定重疊。`ApplyVolume` 算的是
  `Clamp01(1 - (|目前頻率 - 頻道頻率| - 0.5))`——誤差 0.5 以內滿音量、到 1.5 才完全消失，
  所以原版兩台要相距 **3.0** 才聽得乾淨，88–108 共 20，**只放得下 7 台**。

  但那個 0.5 / 1.5 只是常數。`頻道寬度` 把整條曲線按 `k` 收窄之後，乾淨間距變成
  `3k`，於是**任何台數都分得開**——代價是調台越來越精細（真的 FM 收音機就是這樣）。
  預設 `0` = 自動，取 `k = 實際間距 ÷ 3`，剛好讓相鄰的台碰不到彼此。

  | 曲目數 | 間距 | 自動寬度 | 靜音半徑 |
  |---|---|---|---|
  | 7 | 3.33 | ×1（不收窄） | 1.5 |
  | 15 | 1.43 | ×0.476 | 0.71 |
  | 25 | 0.83 | ×0.278 | 0.42 |

  收窄之後 `ApplyVolume` 算出來的東西就不能用了（它算的是遊戲自己那條曲線，
  在原結果上乘倍率救不回來），所以 postfix 會**整條重算**——那段程式本來就
  存在，是 `Boss 出現時繼續播放` 在用的。
- **這是本機的東西。** 頻道是在本機生出來的，同步的只有「你轉到幾點幾」這個數字。
  別人沒裝、或設定的頻道數不一樣，轉到同一個頻率聽到的就不是同一台。
  要和別人一起聽，大家的 `頻道數`、`頻道寬度` 和音樂資料夾都要一致。

### 這幾個設定怎麼互動

三個設定講的是同一件事的不同層面，容易搞混：

| 頻道數 | 一個頻道幾首 | 自動接下一首 | 「下一首」熱鍵 |
|---|---|---|---|
| **0（自動，預設）** | 1 | 沒有作用（沒有下一首可接） | 沒有作用，按了會在 log 說明 |
| −1（不改）或 N < 曲目數 | 多首 | **有用**，自己接著播 | **有用**，跳到下一首 |
| 同步播放開著 | — | 有用 | 一律擋掉（跳過會脫隊） |

換句話說：**「一首歌一個頻率」和「一個頻道排一串歌」是二選一的兩種電台**。
預設是前者；想要後者就把頻道數設成 −1，或一個小於曲目數的數字。

### 自動接下一首（預設開）

曲目本來就是**輪流平均分**給各頻道的（`for (int i = ch; i < Shared.Count; i += totalChannels)`，
所以各頻道的數量最多差一首）。但舊版一個頻道同時只「持有」一首，多出來的要按 F7
才聽得到——歌一多就等於大半的曲目被藏在熱鍵後面。

打開「自動接下一首」之後，一個頻道的曲目串成**一條連續的節目表**，
像真的電台一樣自己接著播下去，不用碰熱鍵。

需要每幀維持的唯一理由就是**換曲**：`Radio.ApplyVolume` 只在轉台和 Boss 事件時
才被呼叫，接不了下一首——這正是舊版要靠 F7 的成因。

這時候 **F7 的意思變成「跳過這首」**：把這個頻道的節目表推到下一首的開頭。
推的是本機的時鐘偏移，所以只有自己會跳。關掉這個設定就回到舊版的行為
（一個頻道持有一首，F7 換持有的那首）。

建議用 `.ogg`——Unity 執行期解碼最穩。載入走 `UnityWebRequestMultimedia` 且
**關閉串流**（`streamAudio = false`），因為遊戲解除靜音時會做
`_channelSource.time = t % clip.length` 的定位，串流 clip 的 seek 不可靠。

### 同步播放（一起聽同一首）

**這是「自動接下一首」的延伸，只差在時鐘。** 節目表本身用本機時鐘就成立
（一個人玩也會自己接下去）；打開同步播放只是把時鐘換成網路時間，讓所有人的節目表對齊。
所以自動接歌預設開著，同步播放預設關著——後者多一個「大家檔案要一樣」的前提。

**遊戲本來就做了一半。** `RadioChannel.ToggleMute` 解除靜音時是這樣定位的：

```csharp
this._channelSource.time = time % this._channelSource.clip.length;
this._channelSource.Play();
```

而傳進去的 `time` 是 `TimeManager.TicksToTime(TickType.Tick)`——**網路時間**，
FishNet 讓每個客戶端的 `Tick` 對齊。所以原版電台本來就是同步的：
兩個人同時轉到同一台，聽到的是同一首歌的同一個位置。

自訂音樂各聽各的，是因為缺三塊：

| 缺的 | 怎麼補 |
|---|---|
| 檔案要一樣 | **補不了**——mod 不能替你發音檔。這是這個功能唯一的前提，只能自己約好 |
| 選曲要一致 | 載入已經是 `StringComparer.OrdinalIgnoreCase` 排序＋輪流分配，所以同樣的檔案會落到同一個頻道；但「同頻道多首時放第幾首」原本是本機游標（F7），一按就散 |
| 換 clip 要重新定位 | 原本是 `src.time = 0f`，直接掉出共同時間軸 |

打開「同步播放」之後，一個頻道的曲目會串成**一條連續的時間軸**（像真的電台節目表）：
總長 = 各首長度相加，`時鐘 % 總長` 落在哪一首的哪一秒就播那裡。
這是純函數——同樣的檔案、同樣的時鐘，每台機器算出來必然一樣，
**不需要新增任何同步狀態**（Harmony 本來也補不上 SyncVar 或 ServerRpc）。

兩個代價，都是本質上的：

- **「下一首」會失效。** 跳過是本機的時鐘偏移，`Sync.Clock` 在同步模式下根本不讀它——
  要讓其他人跟著跳需要一條新的同步訊息，而那正是 Harmony 做不到的事。
  （曾考慮把選曲編碼進 `Radio._frequency` 的低位元來偷渡，但那會跟真正的轉台打架、
  頻率數字也會變得很奇怪，不值得。）
- **對時精度是「同一首同一段」，不是取樣級對齊。** 位置容差設 1 秒，
  超過才校正——每幀硬寫 `AudioSource.time` 會有卡頓聲，而一起聽本來也不需要那麼準。

沒打開時行為不變（本機播放、F7 可以切歌），只是換曲改成定位到 clip 內的共同位置
而不是從 0 開始——那本來就是遊戲自己解除靜音時做的事，所以**單曲頻道即使不開這個
設定也是同步的**。

### 那個「沙沙」聲是故意的

`Radio.ApplyVolume()` 的最後一行：

```csharp
this._noiseSource.volume = (1f - num2) * 0.075f;
```

`num2` 是「最接近的頻道有多準」（0–1），算法是：

```csharp
float d = Mathf.Abs(目前頻率 - 頻道頻率) - 0.5f;
d = Mathf.Clamp01(1f - d);
```

也就是誤差 **0.5 以內完全沒有雜訊**，超過 **1.5 就是全雜訊**——在模擬 FM 收音機的空頻噪音。
聽到沙沙代表沒調準。`雜訊音量倍率` 設 0 就完全關掉。

另外遊戲在 **Boss 登場時會把收音機整個靜音**（頻道和雜訊都歸零後提早 return）。
`Boss 出現時繼續播放` 打開後會把音量重新算一遍讓音樂繼續——
這是唯一需要重算原本那段邏輯的地方，其餘都只是在原結果上乘倍率。

### Boss 被打死之後收音機不會再響（遊戲的 bug，已修）

`Radio` 把 `ApplyVolume` 掛在 `OnGlobalBossDeath` 和 `OnGlobalBossDespawn` 上
（`Radio.cs:240-242`），而 `ApplyVolume` 開頭是
`if (BossManager.Boss) { 全部靜音; return; }`。問題是**兩條收尾路徑的順序不一樣**：

```csharp
// BossManager.OnBossDeath（BossManager.cs:239-252）
onGlobalBossDeath();          // ← 事件先發
...
BossManager.Boss = null;      // ← Boss 之後才清掉

// BossManager.OnBossDespawn（BossManager.cs:300-301）
BossManager.Boss = null;      // ← 這條是先清
onGlobalBossDespawn();
```

所以 **Boss 被打死**時 `ApplyVolume` 看到 `Boss` 還在，又靜音一次就 return，
而之後沒有任何東西會再呼叫它——收音機就永遠不會再響。
**Boss 自己消失**那條反而正常，因為它先清了 `Boss`。

修法是掛 `BossManager.OnBossDeath` 的 postfix：方法回傳時 `Boss` 已經是 null，
這時候再呼叫一次**遊戲自己的** `ApplyVolume` 就好。刻意不自己重算——
那正是 despawn 路徑做的事，走同一條路行為才會完全一致。

重算時**頻率來源要照抄遊戲那一行判斷**：

```csharp
float freq = (Holder && Holder.Owner.IsLocalClient) ? _localFrequency : _frequency.Value;
```

`_localFrequency` 只有本機持有時才是當下的值。別人持有時它落後一拍，因為
`OnFrequencyChange` 是先 `ApplyVolume()`、之後才寫 `_localFrequency`，
而我們的 postfix 就掛在那次 `ApplyVolume` 後面。這裡一開始寫錯了，
`MODDING_CONTEXT.md` 第 6 節收音機那條被推翻的敘述留在原地劃掉當教訓。

**F8 重載會自己銷毀舊的 `AudioClip`。** 它是 `UnityEngine.Object`，從容器移除只是丟掉參照，
而且載入時 `streamAudio = false`（有理由，見上），等於整份解碼常駐記憶體——
不銷毀的話 200MB 的音樂資料夾按幾次 F8 就是幾百 MB 有去無回。
順序是**先讓 `ApplyToAll` 把新 clip 換上去、再 `Destroy` 舊的**，
還掛在某個 `AudioSource` 上的則留到下一輪再試。

## HtF.DazedTools — 遊戲內建 dev 指令的圖形介面

**設計說明在 `HtF.DazedTools/README.md`**（那個 mod 的內容夠多，自己一份）。
一句話版本：遊戲自己有一套 `DazedCommands` 開發指令，但它有幾個必定自踢的 bug，
這個 mod 把修好的版本搬進 BepInEx 並加上 IMGUI 操作介面（預設 Insert）。

它和 `HtF.Guardian` 是同一件事的兩面：一個送這些 ServerRpc、一個擋。
你當房主時兩個一起裝不會打架，房主預設豁免。

## HtF.ConfigMenu — 遊戲內設定管理頁面

按 **F9** 開啟。左邊是插件清單，右邊是該插件的設定，依 section 分組。

**不綁定任何特定 mod。** 它透過 BepInEx 的 `Chainloader.PluginInfos` 列舉
**所有**已載入插件的 `Config.GetConfigEntries()`，所以之後你新增的 mod
會自動出現在清單裡，這個頁面完全不用改。

控件依 `ConfigEntryBase.SettingType` 決定：

| 型別 | 控件 |
|---|---|
| `bool` | 開／關膠囊 |
| `enum` | ◀ ▶ 循環 |
| 數值 + `AcceptableValueRange` | 滑桿 + 數值顯示 |
| 數值（無範圍）、`string` | 文字框 |
| `KeyboardShortcut` | 點一下後按新按鍵（Esc 取消） |
| 其他 | `GetSerializedValue` / `SetSerializedValue` 的原始字串 |

每一項有 `↺` 重設回預設值（已經是預設值時會變灰），每個插件有「全部重設」和
「寫入檔案」。設定改動會即時觸發 `SettingChanged`，所以像 `HtF.HostRules`
那種有訂閱事件的 mod 會立刻套用，不用重開房間。

### 選單按鈕

除了 F9，ESC 暫停選單和主選單各會多出一顆「模組設定」按鈕（兩者都能在設定裡關掉）。

做法是**複製一顆現有的按鈕**再改文字與 `onClick`，不是自己從零建 UI——
這樣字型、配色、hover 音效、選取特效全部自動跟遊戲一致，排版也由原本的 LayoutGroup 接手。

要複製哪一顆**不是用階層路徑猜的**（那種寫法遊戲一改版就壞），而是把場上的
Button 依 parent 分組，挑出「最像主要按鈕直欄」的那一組：

| 比較順序 | 鍵 | 為什麼 |
|---|---|---|
| 1 | `VerticalLayoutGroup` > 其他 `LayoutGroup` > 沒有 | 兩邊的按鈕欄都是垂直 LayoutGroup 排的 |
| 2 | 欄內按鈕數 | 主選單主欄 6 顆，`[Dev]` 側欄只有 3 顆 |
| 3 | 階層路徑、parent 的 sibling index | 純粹拿來打平手，讓結果唯一 |

暫停選單掃 `PauseManager._mainScreen` 底下（含停用物件，選單平常是關著的）；
主選單改全場景掃描並排除 `PauseManager._pauseHolder`，另外濾掉
`CanvasGroup.alpha < 0.1 || !blocksRaycasts` 的隱藏畫面。

### 為什麼按鈕位置以前每次開遊戲都不一樣

**因為挑模板那一步不是決定性的。** 主選單的候選來自
`FindObjectsByType(..., FindObjectsSortMode.None)`，那個順序官方明講是未定義的，
而舊版直接拿「候選清單裡的最後一顆」當模板、再照它的 sibling index 插進去
——每次啟動拿到的是同一欄裡的**不同**按鈕，複製品的位置自然跟著跳。

修法是把整條選法排成決定性的：**欄內先依 sibling index 排序**（畫面上的實際順序），
欄與欄之間用上面那張表的固定鍵比大小。插入位置則由「按鈕位置」設定決定
（最後一顆的上面／整欄最下面／整欄最上面），預設是最後一顆的上面。
算 index 用的是**現有按鈕的實際 sibling index**，不是 0 / `childCount`
——欄位裡常常還夾著標題、分隔線那類非按鈕物件。

兩個必要的善後：

- **拿掉複製品上的本地化元件**（型別名含 `Localize` 的都清掉）。留著的話，
  語系初始化或物件重新啟用時，它會用字典裡的原字串把我們設的文字蓋回去。
- **整個換掉 `onClick`**（`button.onClick = new Button.ButtonClickedEvent()`）。
  prefab 綁死的持久監聽者沒辦法逐條移除，不換掉的話按下去會連原本的
  「離開遊戲」一起觸發。

### 中英雙語

「語言」設定（`自動 / 中文 / 英文`）在這個 mod 裡，**七個 mod 全部跟著它走**。
機制見下面〈中英雙語（全部 mod）〉。這個頁面自己的文字在 `Localization.cs`，
設定項的英文名稱與說明則寫在 `Plugin.Awake` 的 `Loc.Bind` 那幾行上。

設定頁面顯示**別的 mod** 的設定時，是用反射去那個組件裡找 `HtF.Shared.Loc.Term`：
有就用它翻，沒有就顯示原字串。ConfigMenu 因此仍然不認識任何特定 mod。

搜尋是原字串和翻譯後的字兩邊都比，不然英文介面下打英文會搜不到中文 key。

「按鈕文字」留空 = 跟著語言自動切換；填了字就一律用填的。**舊版的預設值
「模組設定」也算留空**——那串字是 1.0.0 自己寫進使用者 `.cfg` 的，改成
「留空 = 自動」之後它在檔案裡看起來就跟使用者親手打的字一樣，
不特別處理的話語言設定會像壞掉（見 `Plugin.MigrateButtonLabel`）。

### 兩個實作細節

**滑桿不即時寫檔。** `ConfigFile.SaveOnConfigSet` 預設是 true，
拖曳時每動一格就會寫一次 `.cfg`。所以拖曳中只更新本地暫存值，
放開滑鼠才真正寫入 `BoxedValue`。

**文字框有獨立暫存區。** 直接綁 `BoxedValue` 的話，打到一半的 `-` 或 `1.`
解析失敗會被吃掉，小數根本打不出來。所以文字先進暫存區，解析成功才提交。

### IMGUI 結構一致性（又一次）

和 DazedTools 同一條規則：**改變版面結構的狀態變更一律延後到 Layout 事件**。
這裡踩到的有：

- 搜尋過濾（項目數量改變）→ 打字即時更新字串，但過濾用 Layout 當下的快照 `_searchApplied`
- 切換左側插件（整份清單換掉）
- **開關與列舉的寫入**——因為使用者可能正在切「顯示原始值」，
  那會讓每一項多出一行，是結構變動

---

## 遊戲版本

以 `Managed/Assembly-CSharp.dll`（遊戲實際載入的那份）為準建置。

遊戲主畫面回報的「1.0.9」來自 `globalgamemanagers` 的 bundleVersion
（`CanvasManager` 讀 `Application.version`），**和 DLL 無關**，
所以不能拿它判斷 DLL 新舊。實測 `Assembly-CSharp.dll` 與備份
`Assembly-CSharp - 1.0.9.dll` 有一批檔案內容不同，但沒有新增或移除任何型別，
而且**這些 mod 掛鉤的每一個方法，兩份都是一樣的**（逐方法比對過）：

| mod | 掛鉤點 | 兩版 |
|---|---|---|
| `HtF.HostRules`（規則） | `ServerSettings`、`PlayerVitals` | 相同 |
| `HtF.HostRules`（釣魚） | `CreatureManager.GetRandomItem`、`BaitInfo`、`ItemInfoWeight`、`Fishable` | 相同 |
| `HtF.RadioMusic` | `Radio.ApplyVolume`、`RadioChannel`、`BossManager.OnBossDeath` | 相同 |
| `HtF.Guardian` | `Server` 的 `RpcReader___*` / `RpcLogic___*`、`Player.BlockInputs`、`Purchasable` 三個子類 | 相同 |
| `HtF.AmmoCounter` | `Weapon.Ammo`、`Attachments.AmmoPerMag` | 相同 |
| `HtF.ConfigMenu` / `HtF.DazedTools` | `Player.BlockInputs`、`DazedCommands.IsServerCommand` | 相同 |

⚠ **檔案層級的 diff 會誇大差異。** 兩次反編譯是分別跑的，dnSpy 對型別的限定寫法
不一定一致——例如 `CreatureManager.GetRandomItem` 整個檔案「不同」，但實際差異只有
一行 `Random.Range(...)` 對 `global::UnityEngine.Random.Range(...)`，那是輸出格式
不是程式碼。要判斷有沒有真的改動，**比方法、而且要看 diff 內容**，
不要只看 `diff -q` 的結果。

反編譯結果**不進版控**（見根目錄 README），要自己產生。本機的擺法是：

| 資料夾 | 內容 |
|---|---|
| `Assembly-CSharp/` | 舊版（含改過的 `DazedCommands.cs`），勿覆蓋 |
| `Assembly-CSharp-1.09/` | 1.0.9 備份 DLL |
| `decompiled-current/` | 目前遊戲實際載入的版本 |

重新產生的方式（dnSpy 的 console 版）：

```bash
dnSpy.Console.exe -o <輸出> --no-sln --no-resources --no-resx --asm-path <乾淨DLL資料夾> <乾淨DLL資料夾>/Assembly-CSharp.dll
```

「乾淨資料夾」= 從 `Managed/` 複製但**排除 `* - *.dll`**（那是 `Assembly-CSharp` 的手動備份，
內部組件名相同，會讓載入器撞名）。要反編譯備份版時，把備份**改名蓋掉**
`Assembly-CSharp.dll` 再跑，它自己的內部參照才會解析到自己。

# AI 上下文：How to Fish 反編譯專案

> 這份文件是給 AI 助手讀的專案上下文，記錄**已驗證**的事實與可重現的方法，
> 目的是讓後續 session 不必重新推導。
> 每條主張都標註了驗證方式；**未驗證的推測一律標記 `[未驗證]`**，不要當成事實使用。
>
> 若要讓 Claude Code 自動載入，把本檔複製為 `CLAUDE.md`。

---

## 1. 這個專案是什麼

Unity 遊戲 **How to Fish**（Steam）的反編譯原始碼，用於 ServerRPC 安全性研究。

| 路徑 | 內容 |
|---|---|
| `Assembly-CSharp/` | 遊戲主組件反編譯結果（271 個 `.cs`） |
| `FishNet.Runtime/` | FishNet 網路框架反編譯結果（386 個 `.cs`）——與 `Assembly-CSharp/` 同層 |
| `Assembly-CSharp/FishNet/Serializing/Generated/` | FishNet 產生的序列化器（**留在 Assembly-CSharp 內**，勿與上一列混淆） |
| `SECURITY_AUDIT_ServerRPC.md` | 既有的 ServerRPC 稽核報告 |
| `DAZED_COMMANDS_手冊.md` | 指令使用手冊 + 完整參數對照表 |
| `AI_CONTEXT.md` | 本文件 |
| `extract_gamedata.py` | 資產抽取腳本（可重跑，見第 6 節） |
| `_dlls/` | 抽取腳本產生的 DLL 快取（178 個，可安全刪除，重跑會重建） |

**遊戲本體安裝路徑**（資產抽取用）：
```
D:\SteamLibrary\steamapps\common\How to Fish\How to Fish\How to Fish_Data
```
Unity 版本 **6000.4.4f1**（Mono 後端，非 IL2CPP）。
---

## 1.5 反編譯結果有三份（2026-08-25 更新）

遊戲已經更新過，`Assembly-CSharp/` 那份是舊的，**不要**再拿它當真實來源。

| 資料夾 | 內容 |
|---|---|
| `Assembly-CSharp/` | 舊版，含手動改過的 `DazedCommands.cs`。保留，勿覆蓋。 |
| `Assembly-CSharp-1.09/` | `Assembly-CSharp - 1.0.9.dll` 備份的反編譯 |
| `decompiled-current/` | 目前遊戲實際載入的版本 ← **查東西看這份** |

用 dnSpy 的 console 版重跑（`C:\Users\leo\Downloads\dnSpy-net-win64\dnSpy.Console.exe`）：

```
dnSpy.Console.exe -o <輸出> --no-sln --no-resources --no-resx --asm-path <乾淨DLL資料夾> <乾淨DLL資料夾>/Assembly-CSharp.dll
```

「乾淨資料夾」= 從 `Managed/` 複製但排除 `* - *.dll`（理由見第 2 節的警告）。
要反編譯**備份版**時，把備份改名蓋掉 `Assembly-CSharp.dll` 再跑，
否則它的內部參照會解析到正本，得到混合的結果。

### 版本差異摘要

1.0.9 備份 → 現行 DLL：66 個檔案有內容差異，**沒有**新增或移除任何型別。

> 主畫面的「遊戲版本 1.0.9」來自 `globalgamemanagers` 的 bundleVersion
> （`CanvasManager` 讀 `Application.version`），**和 DLL 無關**，不能拿來判斷 DLL 新舊。

1.0.7（舊 `Assembly-CSharp/`）→ 1.0.9 之間新增了 `Difficulty` 列舉與整套難度系統：

- `ServerSettings` 多了 `_difficulty` SyncVar，以及靜態的
  `Difficulty` / `HealthMultiplier` / `DamageMultiplier`。
- `OnDifficultyChange` 把乘數設成 簡單 0.75/0.5、普通 1/1、困難 1.25/1.25。
- 消費點：`Creature.MaxHp`（血量乘數）、`PlayerVitals` 的傷害計算（傷害乘數）。
- `SaveManager.CreateServer` 多了 `Difficulty` 參數，`ServerSaveObject` 多了該欄位。

> ⚠ **`ServerSettings.HealthMultiplier` / `DamageMultiplier` 是一行的 auto-property，
> Mono 會把它們 inline 進 `Creature.MaxHp` 這類呼叫端。對這種 getter 下 Harmony patch
> 會靜靜地不生效——沒有錯誤也沒有 log。要改就用反射寫入私有 setter，
> 並在 `OnDifficultyChange` 之後補蓋一次。** 見 `mods/README.md`。

---

> **做 mod 相關的事請先讀 `MODDING_CONTEXT.md`**——BepInEx／Harmony／IMGUI 的
> 硬規則、遊戲端的守衛清單、各系統的掛鉤點都在那裡，這份文件不重複。

## 2. 反編譯源碼不能整體編譯 —— 但單檔可以

**已驗證。** 反編譯結果含有非法 C# 識別字，例如：

```csharp
this.NetworkInitialize___EarlyProjectileManagerAssembly-CSharp.dll_Excuted
pooledWriter.GWrite___PlayerFishNet.Serializing.Generated(owner)
```

識別字中有 `.` 和 `-`（FishNet IL weaving 產生的名稱），整個專案**無法**建置。
不要嘗試 `dotnet build Assembly-CSharp.csproj`——它一定失敗，且與你的改動無關。

### 單檔編譯驗證法（已驗證可用）

對**單一檔案**做真實的語法 + 型別檢查，參照遊戲原本的 DLL：

`$MANAGED` = `D:\SteamLibrary\...\How to Fish_Data\Managed`

> ⚠️ **該資料夾含有 `Assembly-CSharp` 的手動備份副本**，內部組件名與正本相同，
> 會讓載入器丟 `An item with the same key has already been added`。**務必排除。**
>
> **不要用固定檔名比對。** 這個備份檔已經被改過名——先前是
> `Assembly-CSharp - 複製.dll`，後來變成 `Assembly-CSharp - 1.0.9.dll`。
> 可靠的判準是：若 `<Base> - <任何字尾>.dll` 與 `<Base>.dll` 同時存在，前者是備份。
>
> **更不要用 try/except 跳過載入失敗的檔案。** 字典序下
> `Assembly-CSharp - 1.0.9.dll` 排在 `Assembly-CSharp.dll` **之前**
> （空格 `0x20` < 點 `0x2E`），那樣會載入舊備份、跳過正本，
> 然後「成功」產出**錯誤的資料**。
>
> `extract_gamedata.py` 的 `prepare_dlls()` 已正確處理，直接沿用。

```xml
<!-- 2. check.csproj，與待驗證的 .cs 放同一目錄 -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0436;CS1701;CS1702</NoWarn>
  </PropertyGroup>
  <ItemGroup><Compile Include="DazedCommands.cs" /></ItemGroup>
  <!-- 對 Managed/ 下每個 dll 產生 <Reference>，但排除：
       netstandard, mscorlib, System, System.Core, System.* -->
</Project>
```

重點：
- **必須排除 Unity 的 `netstandard.dll`**，它是 facade，會與 SDK 的 BCL 衝突，
  導致大量 `CS0518: 未定義預先定義的類型 'System.String'`。
- `CS0436` 要抑制：待驗證的型別在參照的 `Assembly-CSharp.dll` 裡也存在，屬正常。
- 目前 `DazedCommands.cs` 在此設定下是 **0 錯誤 0 警告**。

---

## 3. FishNet 的踢人機制（除錯「彈回主頁面」的關鍵）

**已驗證**，出自 `FishNet.Runtime/FishNet/Managing/Server/ServerManager.cs`：

| 行號 | 條件 | 後果 |
|---|---|---|
| 1111–1119 | RPC 解析／執行期間**拋出任何例外** | `Kick(KickReason.MalformedData)` |
| 1021–1025 | 單一封包 `> GetMTU(channel)` | `Kick(KickReason.ExploitExcessiveData)` |
| 1062 | 未通過認證就送封包 | `Kick(ExploitAttempt)` |
| 758 | 逾時 | `Kick(UnexpectedProblem)` |

### 這代表什麼

> **伺服器端 RPC 邏輯裡的任何一個 NRE / IndexOutOfRange / KeyNotFound，
> 都會讓「發送那個 RPC 的客戶端」立刻斷線回主畫面。**

所以在客戶端造 RPC 參數時，必須**在送出前**自行驗證伺服器端的前置條件，
因為伺服器不會客氣地忽略壞參數，而是連人一起踢掉。

`Server.cs` 的 `RpcWriter___*` 只做序列化 + `SendServerRpc`，**沒有 host 端的同步 `RunLocally`**
（已驗證，見 `Server.cs:1550`）。因此：
- 在迴圈中送 RPC **不會**造成 `Collection was modified` 例外。
- 但 host 自己送的 RPC 仍會走網路迴路，一樣受上述踢人規則約束。

---

## 4. 已確認的伺服器端 RPC 危險清單

以下全部**已驗證**（`Assembly-CSharp/Server.cs` 的 `RpcLogic___*` 及其下游）。

### 4.1 `index - 1` 陷阱（byte 參數 + int 減法）

`byte` 參數在 `index - 1` 時會提升為 int，所以 **0 變成 −1**（不是 255），直接越界。

| 位置 | 程式碼 | 觸發指令 |
|---|---|---|
| `PlayerInventory.cs:52-55` | `_extraSlotCosts[(int)(index - 1)]` | `/forceunlockpocket 0` |
| `PlayerInventory.cs:750-756` | `_ownedBaits[(int)(index - 1)]` | `/buybaitfree 0` |

兩者的伺服器上界守衛（`A_2 > 5` / `A_2 >= AllBaits.Count`）都**沒有檢查下界**。

已掃過全專案的 `[x - 1]` 索引，其餘（`CloseItemsUI.cs`、`IK.cs`）都在客戶端渲染路徑，
不經 RPC，無風險。`BuyAttachment` 用 `AllAttachments[(int)A_2]`（0 起算 + 上界守衛），安全。

### 4.2 無 null 檢查的靜態單例

`RpcLogic___*` 中直接解參考 `X.Instance` 而未檢查的清單（掃描全部 35 個 RpcLogic 得出）：

| RPC | 單例 | 風險 |
|---|---|---|
| `PlaceBet` | `CasinoManager.Instance` | **高**——離開賭場島即為 null／已銷毀 |
| `UpdateRoulette` | `CasinoManager.Instance` | **高**——同上 |
| `TakeItemFromNpc` | `NPCManager.Instance`、`OnlineIslandManager.Instance` | 中 |
| `AddProjectile(s)`、`ProjectileHitDynamic` | `ProjectileManager.Instance` | 低（遊戲中恆存在） |
| `SendChatMessage` | `OnlineChatManager.Instance` | 低 |
| `SendFinishGame` | `EndGameManager.Instance` | 低（前有島嶼守衛） |
| `RespawnPlayer` | `BoatManager.Instance` | 低 |

> `CasinoManager.Instance` 是**純靜態欄位**（`CasinoManager.cs:342`），
> 只在 `Awake` 指派、銷毀時**不會清空**。離開賭場島後它是「已銷毀但非 null」的
> Unity 物件，存取會丟 `MissingReferenceException`。

### 4.3 字典索引器

`NPCManager.GetShowingQuest(npcID)` 執行 `NPCManager._idToNpc[npcID]`（字典索引器）。
`Server.cs:1961` 的守衛是 `if (A_2 != 255 && !NpcIsHoldingItem(A_2)) return;`
—— **id 為 255 時整個守衛被跳過**，直接走到字典索引 → `KeyNotFoundException`。
`/takenpcitem 255` 是必定自踢。

### 4.4 封包量

`/tpitems` 對每個物品送一個 reliable ServerRpc，伺服器收到後經
`RigidbodySync.cs:259-274` 的 `ServerSetPosRot` 再對**每個客戶端**廣播一個
unreliable ObserversRpc。N 個物品 × M 個玩家的封包擠在同一 tick。
`/hijackitemphysics`、`/detonateall`、`/hitcreature X all` 同理。

### 4.5 待查（未驗證）

`[未驗證]` `Item.RigidbodySync` 在 `SetSyncedSimulator`（`Server.cs:1058`）與
`UpdateItemPosRot`（`Server.cs:1014`）的伺服器端**沒有 null 檢查**，
而 `Item` 上沒有 `[RequireComponent(typeof(RigidbodySync))]`。
整個 codebase 都假設每個 Item 都有這個元件（無任何一處做 null 防護），
但 prefab 不在反編譯結果裡，**無法確認是否真有 Item prefab 缺少它**。
若日後 `/tpitems` 或 `/hijackitemphysics` 仍造成斷線，這是下一個查證點。

---

## 5. `DazedCommands.cs` 的修改摘要

檔案從 1401 行 → 1736 行。**這是唯一被修改的專案檔案。**
原始備份為 session 暫存檔，已不可靠——如需原版請從 `Assembly-CSharp.dll` 重新反編譯。

### 修正的核心 bug

1. **參數切片**：舊碼在無參數時 `array2` 仍含指令名本身，使所有
   `args.Length == 0` 守衛永遠不觸發（`/hitplayer` 會秒殺自己）。
2. **`TryParse` 歸零**：`int damage = 999999; int.TryParse(args[0], out damage);`
   解析失敗時 `out` 被寫入 `default(T)`，預設值全毀。共 10 處。
   已改為 `ParseInt/ParseUInt/ParseByte/ParseFloat/ParseBool` 系列，
   失敗時**保留** fallback，並一律使用 `CultureInfo.InvariantCulture`。
3. **`ResolvePlayer` 找不到就回傳自己** → 改為回傳 `null` + 明確報錯。
4. **`/spoofchat` 的 SteamID 分支是死碼**（因 3 而不可達）→ 已修復。
5. **索引下界**：`/forceunlockpocket` 改為 1–5、`/buybaitfree` 改為 1–（Count−1），
   且 `/buybaitfree` 預設值從 0 改為 1（舊預設會直接自踢）。

### 新增的安全機制

| 常數／方法 | 行 | 用途 |
|---|---|---|
| `MaxBatchTargets = 48` | 495 | 批次指令目標上限 |
| `MaxSpoofedProjectiles = 64` | 499 | 彈丸陣列上限（避免超 MTU） |
| `MaxTargetRange = 60f` | 503 | 生物／物品搜尋半徑 |
| `GetBatchPlayers` / `GetBatchItems` | 670 / 687 | 先快照再送，套用上限 |
| `ResolveSingleTarget` | 652 | 不支援 `all` 的指令會明講 |
| `GetNearestItem` | 712 | 取代「字典第一個」的隨機 fallback |
| `HasConfirmArg` | 579 | `/finishgame`、`/sendfinishgame` 需 `confirm` |

各危險 RPC 送出前已加入前置條件檢查（賭場單例、NPC id、ProjectileManager 等）。

### 刻意的行為改變（非單純修 bug）

- `/finishgame`、`/sendfinishgame` 需附加 `confirm` 才執行。
  移除方式：刪掉這兩個方法裡的 `HasConfirmArg` 判斷。

---

## 6. 遊戲資產抽取方法（可重現）

參數對照表（物品 ID、魚餌、配件、NPC…）**不在原始碼裡**——
`GameInfo` 用 `Resources.LoadAll<Item>("Items")` 在執行期載入，資料在 Unity 資產檔中。

> **已有可直接執行的腳本：`extract_gamedata.py`（repo 根目錄）。**
> 它封裝了下面所有技巧，並已端對端驗證可重跑：
> ```
> venv/Scripts/python.exe extract_gamedata.py 輸出.json
> ```
> 預期輸出：85 個物品（全部有 ID）、18 BaitInfo、7 AttachmentInfo、
> 31 NPC 元件（15 個不重複 ID）、5 SlotInfo、698 筆 Resources 路徑。
> 數字對不上就表示遊戲更新了，需要重新檢查下列假設。
>
> 以下說明的是腳本背後的原理，供修改或除錯時參考。

### 環境

```bash
python -m venv venv
venv/Scripts/python.exe -m pip install UnityPy TypeTreeGeneratorAPI
```
已驗證版本：UnityPy 1.25.3。

### 四個關鍵技巧（都踩過坑）

**1. 型別樹被剝離，須從 DLL 重建**
```python
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
g = TypeTreeGenerator("6000.4.4f1")
g.load_local_dll_folder("_dlls")  # 已排除備份副本的目錄，見第 2 節
env.typetree_generator = g
```
自訂 MonoBehaviour 的 `read_typetree()` 在沒有 generator 時會直接失敗。

**2. `parse_as_object()` 對被剝離的 MonoBehaviour 無效——手動解析固定表頭**

MonoBehaviour 的前 32 bytes 佈局是固定的，不需要型別樹：
```python
def header(o):
    r = o.get_raw_data()
    go_fid, go_pid = struct.unpack_from("<iq", r, 0)    # m_GameObject PPtr
    sc_fid, sc_pid = struct.unpack_from("<iq", r, 16)   # m_Script PPtr
    return (go_fid, go_pid), (sc_fid, sc_pid)
```
（12 bytes PPtr + 1 byte m_Enabled + 3 padding = offset 16 才是 m_Script）
PPtr 的 `fileID == 0` 指同檔，否則指 `assets_file.externals[fileID - 1]`。
把 `m_Script` 解到 `MonoScript`（內建型別，有型別樹）即可拿到 `m_ClassName`。

**3. 要抓齊子類別，必須算繼承的傳遞閉包**

只列直接子類別會漏掉一半。`Item` 的完整閉包有 **23 個類別**：
```
Albatross, AttackingFish, Bird, BowheadWhale, Crab, Creature, DeadPlayer, Disc,
Explosive, Fish, FishingRod, FishingRodCast, FishingRodCrab, Item, Map, Melee,
Piranha, Pufferfish, Radio, RunningFish, Spidercrab, Tool, Weapon
```
只用 12 個類別時只抓到 53/85 個物品。

**4. 型別樹讀到一半炸掉時，把樹截斷到目標欄位**

Unity 序列化是**基底類別欄位在前**，且 `_id` 位在 `Item` 樹的第 13 個節點
（`m_Name` 之後、`LocalizedString _nameLocalized` 之前）。
`LocalizedString` 的產生樹與實際資料不符，會讓完整讀取失敗（69/85 失敗）。
**把節點列表截斷到 `_id` 為止再讀 → 85/85 全部成功。**

```python
def truncated(cls, field):
    flat = flatten(g.get_nodes_up("Assembly-CSharp.dll", cls), [])
    i = next(j for j, n in enumerate(flat) if n.m_Name == field and n.m_Level == 1)
    e = i + 1
    while e < len(flat) and flat[e].m_Level > 1:   # 含複合型別的子節點
        e += 1
    return TypeTreeNode.from_list([
        TypeTreeNode(n.m_Level, n.m_Type, n.m_Name, 0, 0, m_MetaFlag=n.m_MetaFlag)
        for n in flat[:e]])

o.read_typetree(truncated("Item", "_id"), check_read=False)
```

### 要載入哪些檔案

```python
targets = [p for pat in ("*.assets", "globalgamemanagers", "level*")
           for p in glob.glob(os.path.join(DATA, pat))
           if not p.endswith((".resS", ".resource"))]
env = UnityPy.load(*sorted(set(targets)))
```
物品 prefab 主要在 `sharedassets0.assets`（**不在** `resources.assets`）。

### `Resources` 資料夾的真實路徑與載入順序

在 player build 中 `env.container` 是空的。正確來源是 `globalgamemanagers`
裡的 **`ResourceManager`** 物件（內建型別，可直接 `read_typetree()`）：
```python
d = o.read_typetree()          # o.type.name == "ResourceManager"
d["m_Container"]               # [(路徑, PPtr), ...]，共 698 筆
```
`Resources.LoadAll` 的回傳順序**假定**與此容器順序（路徑字典序）一致——
這是推導魚餌索引的依據。`[未驗證]` Unity 未正式保證此順序。

---

## 7. 已抽取的資料

完整表格見 **`DAZED_COMMANDS_手冊.md`**，此處只記來源與陷阱。

| 資料 | 數量 | 來源 |
|---|---|---|
| 物品 ID + 名稱 | 85（ID 0–85，30 為跳號） | Item 閉包 + 截斷樹 |
| 魚餌 | `AllBaits` 共 17 項 | 見下方推導 |
| 配件 | 7 | `AttachmentInfo`，依 `m_Name` 字母排序 |
| NPC ID | 15（0–14） | `NPC._id` |
| 口袋成本 | `_extraSlotCosts = [5,10,25,50,100]` | `PlayerInventory` prefab |
| 船馬達 | 力道 800／1400／2000／3000 | `BoatMotor` |
| 島嶼場景 | Island1–5 + DevIsland | `globalgamemanagers` BuildSettings |

### 物品名稱的查找鍵

`GameInfo.cs:725`：
```csharp
_nameToSpawnable.Add(item.name.Replace(" ", "").ToLower(), item);
```
鍵 = **prefab 的 GameObject 名稱，去空格、全小寫**。
例：`Flying Fish` → `flyingfish`。

### 魚餌索引的推導（易錯，已仔細驗證）

`GameInfo.cs:699-707`：
```csharp
_allBaits.Add(this._emptyBait);                        // 先加入
foreach (BaitInfo b in Resources.LoadAll<BaitInfo>("Baits"))
    if (!this._defaultBaits.Contains(b))               // 排除預設餌
        _allBaits.Add(b);
_allBaits = _allBaits.OrderBy(x => x.Cost).ToList();   // 穩定排序
```
已驗證：
- `_defaultBaits` = `Default Crab Bait`(Ham)、`Default Fish Bait`(French Fry)，**被排除**。
- `_emptyBait` → fileID 2 (`sharedassets0.assets`) pathID **7802** = `baits/empty bait`，
  而它**不在** `_defaultBaits` 內 → **會被加入兩次**。
- 結果：`AllBaits.Count == 17`，索引 0 與 1 都是 Empty Bait。
- `_ownedBaits` 長度 = `AllBaits.Count - 1` = 16（`PlayerInventory.cs:94-97`）。
- 因此 `/buybaitfree` 有效範圍 **1–16**，0 會自踢。

---

## 8. 其他已驗證的細節

- `ItemManager.Items` 是 `public static Dictionary<Transform, Item> { get; } = new(...)`
  （`ItemManager.cs:14`），**永遠不為 null**——原碼中的 `!= null` 檢查都是死碼。
- `GameInfo.GetSpawnable(string)` 回傳**真正的 null**（不是 Unity fake-null），
  所以 `GetSpawnable(x).GetComponent<T>()` 會丟 NRE。
- `Creature : Item`、`DeadPlayer : Item`，所以生物與屍體都在 `ItemManager.Items` 裡。
- `PlayerInventory.BoughtBait()`（`PlayerInventory.cs:768`）的
  `_ownedBaits[_curBait.Value]` 少了 `- 1`，索引 16 會越界——
  但**已確認它沒有任何呼叫者，是死碼**，不會觸發。
- `ProjectileManager` 對 `WeaponInfo.Weapon == null` 有防護
  （`ProjectileManager.cs:104,122`），偽造彈丸不設 `Weapon` 是安全的。
- `VFXManager.Play` 對空字串有防護（`VFXManager.cs:37`）。
- 聊天指令入口：`ChatManager.SendTypedMessage()` → `DazedCommands.IsServerCommand(text)`
  （`ChatManager.cs:72`）。回傳 `true` 會吞掉該訊息不送出。
- 指令閘門是 `ClientSettings.CheatsEnabled`——**純客戶端旗標**，
  伺服器端對這些 `[ServerRpc(RequireOwnership = false)]` 沒有任何權限驗證。
  這正是本專案研究的漏洞本體，不是待修的 bug。

---

## 9. 常見誤判提醒

給後續 session 的避雷清單，這些我都實際踩過：

1. **不要**試圖整體編譯反編譯源碼（見第 2 節）。
2. **不要**從伺服器的上界守衛推斷參數合法範圍——
   必須追到實際使用該索引的那一行（`/forceunlockpocket` 就是這樣誤判成 0–5 的）。
3. **不要**假設 `byte - 1` 會 wrap 成 255；C# 會提升為 int，得到 −1。
4. **不要**只列直接子類別；要算傳遞閉包（見第 6 節技巧 3）。
5. **不要**把 Unity 的 `netstandard.dll` 加進編譯參照。
6. 記得排除 `Assembly-CSharp - 複製.dll`。
7. `OrderBy` 是穩定排序——並列項的先後取決於原始加入順序，
   推導索引時必須先確定加入順序。

---

## 10. 快速定位

| 要找什麼 | 去哪 |
|---|---|
| 指令分派 | `Assembly-CSharp/DazedCommands.cs` 的 `IsServerCommand` |
| ServerRPC 定義與伺服器邏輯 | `Assembly-CSharp/Server.cs`（`RpcLogic___*` 是真正的邏輯） |
| 踢人條件 | `FishNet.Runtime/FishNet/Managing/Server/ServerManager.cs:1000-1210` |
| 物品欄位定義 | `Assembly-CSharp/Item.cs`（`_id` 在 1874 行附近） |
| 背包／魚餌／口袋 | `Assembly-CSharp/PlayerInventory.cs` |
| 靜態遊戲資料表建構 | `Assembly-CSharp/GameInfo.cs:690-730` |

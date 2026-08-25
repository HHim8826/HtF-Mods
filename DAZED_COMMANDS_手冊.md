# DazedCommands 指令手冊

本手冊對應修正後的 `Assembly-CSharp/DazedCommands.cs`。
所有對照表的數值都是從遊戲本體資產（`How to Fish_Data`）與原始碼實際抽取出來的，不是憑印象寫的。

---

## 1. 使用方式

- 在遊戲內**聊天欄**輸入，指令一律以 `/` 開頭。
- 需要 `ClientSettings.CheatsEnabled` 為開啟，否則只會看到 `Only dev is allowed to type commands`。
- 輸入 `/vulnhelp` 或 `/cmdhelp` 可在遊戲內列出所有指令。
- 指令與參數**不分大小寫**。

---

## 2. 通用參數規則

### 2.1 目標參數 `<target>`

凡是寫 `<target>` 的地方，都接受以下五種寫法：

| 寫法 | 意義 |
|---|---|
| `me` 或 `self` | 你自己 |
| （留空，限有預設值的指令） | 你自己 |
| `all` | 全部玩家 |
| 數字（小） | 玩家索引，`0` 是第一位；超出範圍會報錯 |
| 數字（大，17 位） | Steam ID |
| 文字 | Steam 暱稱**模糊比對**（包含即可，不分大小寫） |

> **找不到目標時會明確報錯**（`Couldn't find player <xxx>`），不會像舊版那樣默默改成打你自己。
> 不支援 `all` 的指令收到 `all` 也會明講。

### 2.2 數字格式

- 小數點**一律用 `.`**（例：`1.5`），不要用逗號。
- 參數打錯時會**保留該參數的預設值**，不會變成 0。

### 2.3 安全上限

| 項目 | 上限 | 原因 |
|---|---:|---|
| 批次指令目標數 | 48 | 一次送太多 RPC 會撐爆連線被踢 |
| `/spoofprojectile` 彈丸數 | 64 | 陣列會塞進單一封包，超過 MTU 會被踢 |
| 生物 / 物品搜尋半徑 | 60 公尺 | 避免打到地圖另一頭的東西 |

---

## 3. 指令總覽

### 3.1 生成物品

| 指令 | 參數 | 說明 |
|---|---|---|
| `/spawn <名稱>` | 見 [物品對照表](#41-物品-id-對照表) | 在你面前 2 公尺生成物品 |
| `/spawndead <名稱>` | 同上 | 生成一個已死亡的生物 |
| `/spawndrip <名稱>` | 同上（僅限生物） | 生成 drip 版本的生物 |
| `/spawndripdead <名稱>` | 同上 | 生成已死亡的 drip 生物 |

名稱用「**去掉空格、全小寫**」的形式，例如 `Flying Fish` → `flyingfish`。
名稱中間有空格也可以（`/spawn flying fish` 一樣有效）。

```
/spawn tuna
/spawn sniperrifle
/spawndrip giantpiranha
```

### 3.2 玩家操作

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/hitplayer <target>` | `[傷害] [繞過PvP]` | `999999` `true` | 對玩家造成傷害。繞過 PvP 時攻擊者為 null |
| `/killplayer` | 同上 | | `/hitplayer` 的別名 |
| `/forcerespawn` | `[target]` | 自己 | 強制重生 |
| `/forceresurrect` | `[target]` | 自己 | 復活屍體 |
| `/tpplayer <target>` | `[x y z]` 或 `[void]` | 你的位置 | 傳送玩家。`void` = 掉到 Y −500 |
| `/forceplayerpos <target> <x> <y> <z>` | | | 直接覆寫座標（不支援 `all`） |
| `/forcedropall` | `[target]` | 自己 | 丟掉全部物品 |
| `/removeplayeritem <target>` | | | 移除手上物品（不支援 `all`） |
| `/forcesetafk <target>` | `[true\|false]` | `true` | 標記 AFK（不支援 `all`） |
| `/godmode` | | | 切換無敵 |
| `/spoofchat <target\|SteamID> <訊息>` | | | 用他人身分發言。找不到玩家時會當作 SteamID |

```
/hitplayer all 100 false
/tpplayer 1 void
/tpplayer me 0 50 0
/spoofchat 76561198000000000 哈囉
```

### 3.3 生物與戰鬥

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/hitcreature` | `[傷害] [all]` | `999999` | 攻擊最近的生物。負數 = 治療 |
| `/healcreatures` | 同上 | | `/hitcreature` 的別名 |
| `/killboss` | | | 對 Boss 造成 999999 傷害 |
| `/killallcreatures` | | | 標記全部生物已擊殺 |
| `/killalldripcreatures` | | | 同上，drip 版 |
| `/resetallcreatures` | | | 取消上面的標記 |
| `/resetalldripcreatures` | | | 同上，drip 版 |
| `/forcefinisheating` | `[target]` | 自己 | 吃掉最近的生物並回復 |
| `/oneshot` | | | 切換一擊必殺 |
| `/detonateall` | | | 引爆場上所有炸藥 |
| `/detonateexplosives` / `/detonateexplosive` | | | 同上的別名 |

```
/hitcreature 999999 all
/hitcreature -500
```

### 3.4 經濟與購買

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/addmoney` | | | +9999 |
| `/removemoney` | | | −9999 |
| `/buyfree <ID或名稱>` | | | 免費取得物品（`isFree=true`） |
| `/buyitemfree` / `/freebuy` | 同上 | | 別名 |
| `/buybaitfree` | `[魚餌索引] [價格]` | `1` `0` | 見 [魚餌表](#42-魚餌索引) |
| `/freebait` | 同上 | | 別名 |
| `/buymotorfree` | `[馬達索引] [價格]` | `1` `0` | 見 [馬達表](#45-船馬達索引) |
| `/freemotor` | 同上 | | 別名 |
| `/buyradarfree` | `[價格]` | `0` | 免費雷達 |
| `/freeradar` | 同上 | | 別名 |
| `/forceunlockpocket` | `[槽位索引]` | `1` | 見 [口袋表](#44-口袋槽位)。**只接受 1–5** |
| `/forcebuyattachment` | `[配件索引]` | `0` | 見 [配件表](#43-配件索引) |
| `/forcebuybulletupgrade` | | | 子彈升級 |
| `/forcebuysharpnessupgrade` | | | 近戰銳利度升級 |
| `/takenpcitem` | `[NPC ID]` | `0` | 見 [NPC 表](#46-npc-id)。**不可用 255** |

```
/buyfree sniperrifle
/buyfree 70
/buybaitfree 16 0
/forcebuyattachment 5
```

> 購買類指令的 `價格` 參數傳 `0` 就是免費——這正是這些指令的用途。
> `/forcebuyattachment`、`/forcebuybulletupgrade`、`/forcebuysharpnessupgrade` 作用在你**手上**的武器；手上沒有時會挑**最近**的一把。

### 3.5 物品操控

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/setitemmultiplier` | `[倍率]` | `1000000` | 設定手上物品的分數倍率 |
| `/setmultiplier` | 同上 | | 別名 |
| `/setitemholder` | `[新持有者] [來源玩家]` | 自己 / 自動 | 搶奪他人手上物品 |
| `/stealhelditem` | 同上 | | 別名 |
| `/tpitems` | `[x y z]` | 你的位置 | 把物品拉到指定座標（上限 48 個） |
| `/gatheritems` | 同上 | | 別名 |
| `/hijackitemphysics` | | | 奪取物品物理模擬權（上限 48 個） |
| `/grillhelditem` | | | 用岩漿烤手上物品 |
| `/grillitemlava` / `/lavacook` | | | 別名 |
| `/grill` | | | 解鎖烤肉架 |

### 3.6 船

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/boat` | | | 解鎖船 |
| `/setboatdriver` | `[target\|none]` | 自己 | 指定駕駛。`none` = 沒有駕駛 |
| `/steerboat` | `[x] [y]` | `0` `1` | 直接送出操舵輸入 |
| `/sendboatinput` | 同上 | | 別名 |

### 3.7 賭場

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/forceplacebet` | `[顏色索引]` | `0` | 見 [賭盤顏色](#47-賭盤顏色) |
| `/spoofroulette` | `[輪盤角度]` | `180` | 偽造輪盤畫面 |
| `/slots <物品名> <外觀索引>` | | | 設定拉霸作弊外觀。物品名填非物品字串則指向船 |

> **賭場兩個指令只能在賭場島使用。** 不在賭場時指令會擋下並提示——因為伺服器端沒有做 null 檢查，硬送出去會讓你被踢線。

### 3.8 遊戲流程與存檔

| 指令 | 參數 | 說明 |
|---|---|---|
| `/nextisland` | | 前往下一座島 |
| `/previsland` | | 前往上一座島 |
| `/allskins` | | 解鎖全部外觀 |
| `/noskins` | | 鎖上全部外觀 |
| `/unlockachievements` | | 解鎖全部成就 |
| `/lockachievements` | | 鎖上全部成就 |
| `/showkillscores` | | 顯示全部擊殺分數獎勵 |
| `/finishgame confirm` | | **結束整場遊戲**，跑製作人員名單 |
| `/sendfinishgame confirm` | | 同上，透過 RPC（只在第 5 島有效） |
| `/forcefinishgame confirm` | | `/sendfinishgame` 的別名 |

> ⚠️ **`/finishgame` 和 `/sendfinishgame` 會結束所有人的遊戲。**
> 這就是你之前「彈回主頁面」的其中一個原因——它不是 bug，是指令本來的功能。
> 現在必須加上 `confirm` 才會執行。

### 3.9 投射物

| 指令 | 參數 | 預設 | 說明 |
|---|---|---|---|
| `/spoofprojectile` | `[歸屬玩家] [數量]` | 自己 `1` | 偽造彈丸來源，最多 64 發 |
| `/spoofprojectilehit` | `[彈丸ID] [歸屬玩家]` | `0` 自己 | 偽造命中歸屬 |

---

## 4. 參數對照表

### 4.1 物品 ID 對照表

`/spawn` 用「`/spawn` 用名稱」欄；`/buyfree` 兩欄都可以（ID 或名稱）。

| ID | `/spawn` 用名稱 | 原始名稱 | 類型 |
|---:|---|---|---|
| 0 | `angelfish` | Angelfish | 魚 |
| 1 | `anglerfish` | Anglerfish | 攻擊性魚 |
| 2 | `bass` | Bass | 魚 |
| 3 | `blobfish` | Blobfish | 攻擊性魚 |
| 4 | `bluegill` | Bluegill | 魚 |
| 5 | `albatross` | Albatross | 信天翁 |
| 6 | `blueshark` | Blue Shark | 攻擊性魚 |
| 7 | `bowheadwhale` | Bowhead Whale | 弓頭鯨 |
| 8 | `theoldpike` | The Old Pike | 攻擊性魚 |
| 9 | `giantpiranha` | Giant Piranha | 食人魚 |
| 10 | `goblinshark` | Goblin Shark | 攻擊性魚 |
| 11 | `mutatedbowheadwhale` | Mutated Bowhead Whale | 弓頭鯨 |
| 12 | `pufferfish` | Pufferfish | 河豚 |
| 13 | `spidercrab` | Spidercrab | 蜘蛛蟹 |
| 14 | `sunfish` | Sunfish | 魚 |
| 15 | `tuna` | Tuna | 攻擊性魚 |
| 16 | `bowlfish` | Bowlfish | 魚 |
| 17 | `catfish` | Catfish | 魚 |
| 18 | `clam` | Clam | 生物 |
| 19 | `clownfish` | Clownfish | 魚 |
| 20 | `cod` | Cod | 魚 |
| 21 | `browncrab` | BrownCrab | 螃蟹 |
| 22 | `dripper` | Dripper | 逃跑魚 |
| 23 | `eel` | Eel | 攻擊性魚 |
| 24 | `flyingfish` | Flying Fish | 魚 |
| 25 | `footsnail` | FootSnail | 生物 |
| 26 | `gar` | Gar | 攻擊性魚 |
| 27 | `goby` | Goby | 魚 |
| 28 | `goldfish` | Goldfish | 魚 |
| 29 | `halibut` | Halibut | 魚 |
| 31 | `leech` | Leech | 生物 |
| 32 | `lobster` | Lobster | 螃蟹 |
| 33 | `mackerel` | Mackerel | 魚 |
| 34 | `needlefish` | Needlefish | 魚 |
| 35 | `oarfish` | Oarfish | 魚 |
| 36 | `parrotfish` | Parrotfish | 攻擊性魚 |
| 37 | `perch` | Perch | 魚 |
| 38 | `pike` | Pike | 魚 |
| 39 | `piranha` | Piranha | 攻擊性魚 |
| 40 | `redsnapper` | Red Snapper | 攻擊性魚 |
| 41 | `salmon` | Salmon | 魚 |
| 42 | `seaurchin` | Sea Urchin | 攻擊性魚 |
| 43 | `seagull` | Seagull | 鳥 |
| 44 | `seahorse` | Seahorse | 魚 |
| 45 | `sengarat` | Sengarat | 魚 |
| 46 | `shrimp` | Shrimp | 魚 |
| 47 | `stonefish` | Stonefish | 攻擊性魚 |
| 48 | `superdwarffish` | Superdwarf Fish | 魚 |
| 49 | `tigerfish` | Tigerfish | 攻擊性魚 |
| 50 | `triggerfish` | Triggerfish | 魚 |
| 51 | `voxelfish` | Voxel Fish | 魚 |
| 52 | `yellowboxfish` | Yellow Boxfish | 魚 |
| 53 | `deadplayer` | DeadPlayer | 屍體 |
| 54 | `assaultrifle` | Assault Rifle | 槍械 |
| 55 | `badball` | Badball | 道具 |
| 56 | `beer` | Beer | 生物 |
| 57 | `brassknuckles` | Brass Knuckles | 近戰 |
| 58 | `brassknucklespackedbackup` | BrassKnucklesPackedBackup | 近戰 |
| 59 | `crabfishingrod` | Crab Fishing Rod | 蟹竿 |
| 60 | `dynamite` | Dynamite | 爆裂物 |
| 61 | `fishingrod` | Fishing Rod | 魚竿 |
| 62 | `disc` | Disc | 飛盤 |
| 63 | `knife` | Knife | 近戰 |
| 64 | `knifepackedbackup` | KnifePackedBackup | 近戰 |
| 65 | `map` | Map | 地圖 |
| 66 | `pistol` | Pistol | 槍械 |
| 67 | `radio` | Radio | 收音機 |
| 68 | `shotgun` | Shotgun | 槍械 |
| 69 | `smg` | Smg | 槍械 |
| 70 | `sniperrifle` | Sniper Rifle | 槍械 |
| 71 | `snowball1` | Snowball1 | 道具 |
| 72 | `snowball2` | Snowball2 | 道具 |
| 73 | `snowball3` | Snowball3 | 道具 |
| 74 | `albatrosshead` | Albatross Head | 道具 |
| 75 | `giantpiranhaskeleton` | GiantPiranhaSkeleton | 道具 |
| 76 | `pufferfishfin` | Pufferfish Fin | 道具 |
| 77 | `spidercrabshell` | Spidercrab Shell | 道具 |
| 78 | `whalefin` | Whale Fin | 道具 |
| 79 | `rockcrab` | RockCrab | 螃蟹 |
| 80 | `bingbong` | BingBong | 攻擊性魚 |
| 81 | `whalemeat` | WhaleMeat | 生物 |
| 82 | `crabmeat` | CrabMeat | 生物 |
| 83 | `pufferfishmeat` | PufferfishMeat | 生物 |
| 84 | `albatrossmeat` | AlbatrossMeat | 生物 |
| 85 | `piranhameat` | PiranhaMeat | 生物 |

> 註：ID 30 在遊戲資料中不存在（跳號）。
> `BrassKnucklesPackedBackup`(58)、`KnifePackedBackup`(64) 是備份用資源，正常遊玩用不到。

### 4.2 魚餌索引

`/buybaitfree <索引> <價格>`

> ⚠️ **索引 0 會讓你被踢回主畫面。** 伺服器的 `ServerBoughtBait` 執行 `_ownedBaits[index - 1]`，
> 減法在 int 運算下讓 0 變成 −1，直接丟例外。索引 0 本來也就是「沒有餌」，不是可購買的項目。
> **有效範圍 1–16**，指令現在會自己擋掉 0。

| 索引 | 名稱 | 售價 | 說明 |
|---:|---|---:|---|
| 0 | *(沒有餌)* | 0 | **不可用，會踢線** |
| 1 | *(沒有餌，重複項)* | 0 | 買了沒有意義 |
| 2 | Fish Bucket | 0 | 釣大 Boss |
| 3 | Testing | 0 | 測試用 |
| 4 | Hot Dog | 1 | 釣較大的甲殼類 |
| 5 | Leech Bait | 1 | 釣第 2 島 Boss |
| 6 | Beginner Lure | 3 | 釣初階魚 |
| 7 | Empty Beer Can | 8 | 釣 Boss |
| 8 | Standard Lure | 15 | 釣標準魚 |
| 9 | Beginner Boss Lure | 40 | 釣初階小 Boss |
| 10 | Professional Lure | 50 | 釣專業魚 |
| 11 | Carrot | 100 | 釣第 2 島 Boss |
| 12 | Standard Boss Lure | 280 | 釣標準小 Boss |
| 13 | Coconut | 350 | — |
| 14 | Scientific Lure | 500 | 釣科學級魚 |
| 15 | Professional Boss Lure | 1200 | 釣專業小 Boss |
| 16 | Scientific Boss Lure | 5800 | 釣科學級小 Boss |

順序推導自 `GameInfo`：先加入 `_emptyBait`，再載入 `Resources/Baits`（排除 `_defaultBaits` 的
Ham 與 French Fry），最後以售價**穩定排序**。Empty Bait 因為不在 `_defaultBaits` 內而被加入兩次，
這就是索引 0 和 1 重複的原因。

### 4.3 配件索引

`/forcebuyattachment <索引>`（依資源名稱字母排序）

| 索引 | 名稱 | 效果 |
|---:|---|---|
| 0 | Compensator | Moderately reduces recoil |
| 1 | Extended Magazine | Increases ammo count in magazine |
| 2 | Iron Sight | Removes any sight attached to weapon |
| 3 | Laser Sight | Easier to hipfire by showing where weapon is pointing |
| 4 | Red Dot Sight | Easier to hit targets while aiming |
| 5 | Sniper Scope | Easier to hit targets far away while aiming |
| 6 | Suppressor | Significantly reduces recoil |

### 4.4 口袋槽位

`/forceunlockpocket <索引>`

> ⚠️ **索引 0 會讓你被踢回主畫面。** 伺服器的 `GetExtraSlotCost` 執行 `_extraSlotCosts[index - 1]`，
> 0 會變成 −1 而丟 `IndexOutOfRangeException`。**有效範圍 1–5**，指令現在會自己擋掉 0。

| 索引 | 解鎖的口袋 | 此指令實際扣款 |
|---:|---|---:|
| 0 | — | **不可用，會踢線** |
| 1 | Satchel（背包） | 5 |
| 2 | Second Pocket | 10 |
| 3 | Third Pocket | 25 |
| 4 | Fourth Pocket | 50 |
| 5 | Fifth Pocket | 100 |

扣款金額取自 `PlayerInventory._extraSlotCosts = [5, 10, 25, 50, 100]`。
商店 UI 顯示的價格（24 / 80 / 260 / 600 / 1400）是另一條購買路徑，與此指令無關。

### 4.5 船馬達索引

`/buymotorfree <索引> <價格>`

| 索引 | 馬達 | 推力 |
|---:|---|---:|
| 0 | 小型 | 800 |
| 1 | 中型 | 1400 |
| 2 | 大型 | 2000 |
| 3 | 大型（強化） | 3000 |

> 馬達**只能升級不能降級**：伺服器只接受比目前更高的索引，送出較低的索引不會有任何反應。
> 索引會被夾在實際馬達數量範圍內，所以填過大的值等同填最大值。

### 4.6 NPC ID

`/takenpcitem <ID>`

| ID | NPC |
|---:|---|
| 0 | Default NPC |
| 1 | Lighthouse NPC |
| 2 | Swamp Daughter NPC |
| 3 | Swamp NPC |
| 4 | Grillmaster NPC |
| 5 | Kiosk NPC |
| 6 | Pufferfish NPC |
| 7 | Roulette NPC |
| 8 | Seagull NPC |
| 9 | Slots NPC |
| 10 | StoreClerc NPC |
| 11 | WeaponSeller NPC |
| 12 | Military NPC |
| 13 | Military NPC 2 |
| 14 | Whale NPC |

> **不要用 255。** 伺服器在 id 為 255 時會跳過自己的檢查並直接查字典，丟出 `KeyNotFoundException`，結果就是你被踢線。現在指令會擋下這個值。

### 4.7 賭盤顏色

`/forceplacebet <索引>`

| 索引 | 顏色 |
|---:|---|
| 0 | 黑 (Black) |
| 1 | 紅 (Red) |
| 2 | 綠 (Green) |

> 超出範圍的值伺服器會自動夾住，不會出錯。

### 4.8 島嶼

`/nextisland`、`/previsland` 在 0–4 之間循環。

| 索引 | 場景 |
|---:|---|
| 0 | Island1 |
| 1 | Island2 |
| 2 | Island3 |
| 3 | Island4 |
| 4 | Island5（最終島，`/sendfinishgame` 只在這裡有效）|
| 5 | DevIsland（不在循環內） |

### 4.9 傷害類型

`/hitplayer` 固定使用類型 `1`（Generic）。列出供參考：

| 值 | 類型 |
|---:|---|
| 0 | NoEffect（無效果）|
| 1 | Generic（一般）|
| 2 | Penetration（穿刺）|
| 3 | Bite（咬）|

---

## 5. 會讓你被踢回主畫面的操作

FishNet 的伺服器把 RPC 處理包在 try/catch 裡，**任何例外都會立刻把發送者踢線**
（`ServerManager.ParseReceived` → `Kick(KickReason.MalformedData)`）；封包超過 MTU 則會觸發 `ExploitExcessiveData`。

下面這些在修正前都會踢你，現在已經在送出前擋下：

| 操作 | 伺服器端會發生什麼 | 現況 |
|---|---|---|
| `/takenpcitem 255` | `KeyNotFoundException` | 已擋下並提示 |
| 不在賭場島用 `/forceplacebet`、`/spoofroulette` | `CasinoManager.Instance` 為 null / 已銷毀 | 已擋下並提示 |
| `/spoofprojectile me 999999` | 封包超過 MTU、甚至 OOM | 已限制為 64 |
| `/tpitems`、`/hijackitemphysics`、`/detonateall`、`/hitcreature X all` | 單幀數百個 RPC 撐爆傳輸佇列 | 已限制為 48 |
| `/forceunlockpocket 0` | `_extraSlotCosts[-1]` → `IndexOutOfRangeException` | 已限制為 1–5 |
| `/buybaitfree 0` | `_ownedBaits[-1]` → 例外 | 已限制為 1–16 |
| `/finishgame`、`/sendfinishgame` | 正常結束遊戲（非錯誤） | 需要 `confirm` |

如果之後還遇到不明斷線，下一個要查的點是 `Item.RigidbodySync`：
`SetSyncedSimulator` 與 `UpdateItemPosRot` 的伺服器端沒有對它做 null 檢查，而 `Item` 上沒有 `[RequireComponent]`。若某個物品 prefab 缺少這個元件，`/tpitems` 或 `/hijackitemphysics` 碰到它就會觸發 NRE。

---

## 6. 資料來源

| 內容 | 來源 |
|---|---|
| 指令語法與預設值 | `Assembly-CSharp/DazedCommands.cs` |
| 伺服器端行為與踢人條件 | `Server.cs`、`FishNet.Runtime/FishNet/Managing/Server/ServerManager.cs` |
| 物品 ID / 名稱 | `resources.assets` + `sharedassets*.assets`（型別樹由 `Assembly-CSharp.dll` 重建） |
| 魚餌 / 配件 / NPC / 口袋 | 同上 |
| 列舉（顏色、傷害類型等） | `BetColor.cs`、`DamageType.cs` |
| 島嶼 | `globalgamemanagers` 的 BuildSettings |

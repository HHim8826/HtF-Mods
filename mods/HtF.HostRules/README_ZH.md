# HtF Host Rules（房主規則）

[English](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HostRules/README.md) | 繁體中文

把遊戲的三段式難度換成一組可以自由轉的旋鈕，再加上房主端的規則：友軍傷害、一擊必殺、
飢餓、回血、中毒、著火、玩家互打傷害、復活數值。

**只有房主要裝。** 它動到的每一件事都在伺服器端算——傷害結算、飢餓與回血的 tick。
裝在純客戶端上完全不會執行到，沒有好處也沒有壞處。房間裡其他人不用裝。

改設定立刻生效，不用重開房間。

## 安裝

**用 mod 管理器（建議）。** 從 Thunderstore Mod Manager 或 r2modman 安裝，按 Start modded。
BepInEx 會跟著一起裝。

**手動。** 先裝 [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)，
再把 `HtF.HostRules.dll` 放進 `BepInEx/plugins/`。

## 設定

`BepInEx/config/htf.hostrules.cfg`，第一次執行後產生。也可以在遊戲裡用
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu)（F9）直接調。

倍率是作用在遊戲原本的預設值上的，所以下表把原本的數字一併列出來，你才看得出「1」是多少。

### 難度乘數

| 項目 | 預設 | |
|---|---|---|
| 覆寫難度乘數 | 關 | 打開後，下面兩個取代遊戲的 簡單／普通／困難 |
| 生物血量倍率 | 1.0 | 作用於 `Creature.MaxHp`。遊戲原本：簡單 0.75、普通 1、困難 1.25 |
| 玩家受傷倍率 | 1.0 | 玩家受到的所有傷害。遊戲原本：簡單 0.5、普通 1、困難 1.25 |

### 規則

| 項目 | 預設 | |
|---|---|---|
| 友軍傷害 | 不變 | 強制覆寫房間設定裡的友傷開關 |
| 一擊必殺 | 不變 | 強制開啟時，近戰／拳頭／子彈傷害固定 99999 |

### 玩家數值

| 項目 | 預設 | 它縮放的原本行為 |
|---|---|---|
| 飢餓速度倍率 | 1.0 | 每 300 tick 掉 1 點飽食 |
| 飢餓扣血倍率 | 1.0 | 飽食歸零後每 150 tick 扣 5 |
| 回血速度倍率 | 1.0 | 每 100 tick 回一次 |
| 回血量倍率 | 1.0 | 每次回 5 |
| 中毒傷害倍率 | 1.0 | 每 100 tick 扣 5 |
| 著火傷害倍率 | 1.0 | 每 50 tick 扣 10 |
| 玩家對玩家傷害倍率 | −1 | 絕對值覆寫，−1 = 不改。遊戲預設 0.25 |
| 復活後生命 | −1 | 絕對值覆寫，−1 = 不改。遊戲預設 25 |
| 復活後飽食 | −1 | 絕對值覆寫，−1 = 不改。遊戲預設 10 |
| 受傷後無敵秒數 | −1 | 秒。−1 = 不改。遊戲預設 0.25 |

### 為什麼 .cfg 裡是中文

`.cfg` 裡的分區名與項目名不管切換到哪個語言都維持中文。它們是**識別字**不是顯示字：
BepInEx 靠它們找回你存的值，翻譯它們等於把每個調好的設定當成全新項目重生，
使用者的值會全部掉回預設。上表列的是遊戲內設定頁面顯示的名稱。

## 刻意做不到的事

它不建立任何新的同步狀態。FishNet 的 `SyncVar` 是編譯期 IL weaving 產生的，Harmony 補不上去——
所以這個 mod 只改「伺服器本來就會算出來的結果」。這也正是它只能是房主專屬、而不是讓客戶端
自己選的原因。

## 相容性

- 《How to Fish》1.0.9，Unity 6000.4.4f1（Mono）
- BepInEx 5.4.23.5，Harmony 2.9

難度旋鈕接的是 `ServerSettings` 上的靜態難度乘數。patch 目標在啟動時會驗證一次，
遊戲改版把它們搬走的話會在 BepInEx log 留一行警告，不會默默失效。

## 連結

- [原始碼，以及另外七個 HtF mod](https://github.com/HHim8826/HtF-Mods)
- [更新記錄](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HostRules/CHANGELOG.md)
- MIT 授權

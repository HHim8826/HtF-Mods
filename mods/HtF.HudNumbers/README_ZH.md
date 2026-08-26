# HtF HUD Numbers（HUD 數值化）

[English](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HudNumbers/README.md) | 繁體中文

把《How to Fish》的 HUD 變成實際數字：血量、飽食、中毒、著火、身上的錢、手上的物品，
以及準心正指著的東西。

**只有你自己要裝。** 沒有任何 Harmony patch，也不送任何封包——它只讀值然後畫出來。
在別人的房間裡用不會影響任何人。

## 顯示什麼

| 分區 | 內容 |
|---|---|
| 生命與飽食 | 血量、飽食、中毒、著火的實際數值 |
| 金錢 | 目前持有金額 |
| 手上物品 | 名稱、售價、重量、熟度、分數倍率 |
| 準心指向 | 看向的生物／物品的血量與數值 |

四項各自可以單獨關掉。

## 安裝

**用 mod 管理器（建議）。** 從 Thunderstore Mod Manager 或 r2modman 安裝，按 Start modded。
BepInEx 會跟著一起裝。

**手動。** 先裝 [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)，
再把 `HtF.HudNumbers.dll` 放進 `BepInEx/plugins/`。

遊戲中按 **F6** 開關面板。

## 設定

`BepInEx/config/htf.hudnumbers.cfg`，第一次執行後產生。也可以在遊戲裡用
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu)（F9）直接調。

| 分區 | 項目 | 預設 | |
|---|---|---|---|
| 一般 | 啟用 | 開 | 總開關 |
| | 開關鍵 | F6 | |
| | 暫停時隱藏 | 開 | 打字時、或遊戲把 UI 關掉時也一併隱藏 |
| 版面 | 位置 | 左上 | |
| | 水平邊距 | 12 | 距離該角落的像素 |
| | 垂直邊距 | 12 | |
| | 縮放 | 1.0 | 0.5 – 2.5 |
| | 字型 | Microsoft JhengHei UI | 系統字型名稱。留空 = Unity 內建字型，中文會變方塊 |
| 內容 | 生命與飽食 | 開 | |
| | 金錢 | 開 | |
| | 手上物品 | 開 | |
| | 準心指向 | 開 | |
| | 準心射線距離 | 60 | 公尺，5 – 300 |

### 為什麼 .cfg 裡是中文

`.cfg` 裡的分區名與項目名不管切換到哪個語言都維持中文。它們是**識別字**不是顯示字：
BepInEx 靠它們找回你存的值，翻譯它們等於把每個調好的設定當成全新項目重生，
使用者的值會全部掉回預設。上表列的是遊戲內設定頁面顯示的名稱。

## 語言

介面中英雙語，跟著
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu)
的「語言」設定走（自動／中文／英文）。沒裝那個 mod 就自己跟著遊戲語系，
即時切換不用重開遊戲。

## 相容性

- 《How to Fish》1.0.9，Unity 6000.4.4f1（Mono）
- BepInEx 5.4.23.5，Harmony 2.9

## 連結

- [原始碼，以及另外六個 HtF mod](https://github.com/HHim8826/HtF-Mods)
- [更新記錄](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HudNumbers/CHANGELOG.md)
- MIT 授權

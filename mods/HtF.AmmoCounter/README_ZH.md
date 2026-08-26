# HtF Ammo Counter（剩餘子彈）

[English](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.AmmoCounter/README.md) | 繁體中文

顯示手上槍械還剩幾發——數字、一排圓點，或兩者並列。

**只有你自己要裝。** 沒有任何 Harmony patch，也不送任何封包：只讀 `Weapon.Ammo` 與配件上的
彈匣容量再畫出來。在別人的房間裡用不會影響任何人。

沒有「備用彈藥」那個數字，是因為這遊戲沒有備彈的概念——補彈直接把彈匣填滿。
所以顯示只有「彈匣內／彈匣容量」，而容量會跟著擴充彈匣配件變。

## 安裝

**用 mod 管理器（建議）。** 從 Thunderstore Mod Manager 或 r2modman 安裝，按 Start modded。
BepInEx 會跟著一起裝。

**手動。** 先裝 [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)，
再把 `HtF.AmmoCounter.dll` 放進 `BepInEx/plugins/`。

遊戲中按 **F10** 開關。

## 設定

`BepInEx/config/htf.ammocounter.cfg`，第一次執行後產生。也可以在遊戲裡用
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu)（F9）直接調。

| 分區 | 項目 | 預設 | |
|---|---|---|---|
| 一般 | 啟用 | 開 | 總開關 |
| | 開關鍵 | F10 | |
| | 暫停時隱藏 | 開 | 打字時、或遊戲把 UI 關掉時也一併隱藏 |
| | 瞄準時隱藏 | 關 | 舉槍瞄準（ADS）時暫時隱藏，不擋狙擊鏡 |
| 版面 | 位置 | 準心下方 | 或畫面四角之一 |
| | 水平邊距 | 0 | 像素 |
| | 垂直邊距 | 90 | |
| | 縮放 | 1.0 | 0.5 – 3.0 |
| | 字型 | Microsoft JhengHei UI | 系統字型名稱。留空 = Unity 內建字型，中文會變方塊 |
| 顯示 | 樣式 | 數字與圓點 | 數字／圓點／兩者 |
| | 顯示彈匣容量 | 開 | 顯示成 `7 / 8` 而不是 `7` |
| | 顯示武器名稱 | 關 | |
| | 裝填提示 | 開 | 換彈匣時顯示 |
| | 低彈量門檻 | 0.34 | 剩餘比例低於此值轉警示色。0 = 只有空彈才變色 |
| | 空彈閃爍 | 開 | |

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
- [更新記錄](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.AmmoCounter/CHANGELOG.md)
- MIT 授權

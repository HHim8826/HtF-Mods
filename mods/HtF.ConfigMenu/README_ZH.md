# HtF Config Menu（遊戲內設定頁面）

[English](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.ConfigMenu/README.md) | 繁體中文

給 BepInEx mod 用的遊戲內設定管理頁面，並在暫停選單與主選單各插一顆按鈕。按 **F9** 開啟。

**它不綁定 HtF 這幾個 mod。** 它是透過 BepInEx 的 `Chainloader.PluginInfos` 列舉**所有**已載入
插件的設定，所以你之後裝的任何 mod 都會自己出現，不用等這裡更新。對方有提供雙語設定名稱就
跟著你的語言顯示，沒有的話就顯示它自己宣告的名字。

**只有你自己要裝。** 它只讀寫設定檔，不碰遊戲狀態也不送封包。頁面開著的時候會擋掉本機玩家的
輸入，免得在後面亂走亂開槍。

## 安裝

**用 mod 管理器（建議）。** 從 Thunderstore Mod Manager 或 r2modman 安裝，按 Start modded。
BepInEx 會跟著一起裝。

**手動。** 先裝 [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)，
再把 `HtF.ConfigMenu.dll` 放進 `BepInEx/plugins/`。

## 語言設定的擁有者就是它

另外七個 HtF mod 的介面都是中英雙語，而它們讀的都是**這一個設定**。在這裡把「語言」調好，
全部即時跟著換，不用重開遊戲。「自動」＝跟著遊戲本身的語系走。

沒裝這個 mod 的話，其他 mod 會自己跟著遊戲語系，功能不會壞——只是少了手動覆寫的開關。

## 設定

`BepInEx/config/htf.configmenu.cfg`，第一次執行後產生。

| 分區 | 項目 | 預設 | |
|---|---|---|---|
| 介面 | 語言 | 自動 | 自動／中文／英文。每個 HtF mod 都跟著它 |
| | 開關鍵 | F9 | |
| | 縮放 | 1.0 | 0.6 – 2.0 |
| | 字型 | Microsoft JhengHei UI | 系統字型名稱。留空 = Unity 內建字型，中文會變方塊 |
| | 顯示原始值 | 關 | 顯示每個設定寫進 `.cfg` 的實際字串，除錯用 |
| 選單按鈕 | 暫停選單顯示按鈕 | 開 | |
| | 主選單顯示按鈕 | 開 | |
| | 按鈕文字 | *留空* | 留空＝跟著語言自動切換。填了字就一律用填的 |
| | 按鈕位置 | 最後一顆的上面 | 或整欄最上／最下 |
| | 診斷：印出按鈕分組 | 關 | 把場上所有按鈕依父物件分組印進 BepInEx log。遊戲改版導致按鈕插不進去時打開它 |

### 為什麼 .cfg 裡是中文

`.cfg` 裡的分區名與項目名不管切換到哪個語言都維持中文。它們是**識別字**不是顯示字：
BepInEx 靠它們找回你存的值，翻譯它們等於把每個調好的設定當成全新項目重生，
使用者的值會全部掉回預設。上表列的是設定頁面顯示的名稱。

## 相容性

- 《How to Fish》1.0.9，Unity 6000.4.4f1（Mono）
- BepInEx 5.4.23.5，Harmony 2.9

它唯一的 patch 是 `Player.BlockInputs` 的 postfix，用來在視窗開著時擋輸入。

## 連結

- [原始碼，以及另外七個 HtF mod](https://github.com/HHim8826/HtF-Mods)
- [更新記錄](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.ConfigMenu/CHANGELOG.md)
- MIT 授權

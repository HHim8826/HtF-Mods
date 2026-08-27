# HtF-Mods — How to Fish 的七個 BepInEx mod

七個各自獨立的 mod：HUD 數值化、剩餘子彈、反外掛驗證層、
房主規則與釣魚生態、收音機自訂音樂與同步收聽、遊戲內設定頁面，以及一個指令工具視窗。
**介面中英雙語**，跟著遊戲語系自動切換。

Seven standalone BepInEx mods for *How to Fish*: HUD numbers, an ammo counter, a
host-side anti-cheat layer, host rules and fishing ecology, custom radio music,
an in-game settings page, and a command tool window.
**The UI is bilingual (繁體中文 / English)** and follows the game's own
language setting automatically.

| 專案 | Thunderstore | GUID | 誰要裝 | 做什麼 |
|---|---|---|---|---|
| `HtF.HudNumbers` | `HtF_HudNumbers` | `htf.hudnumbers` | 只有你自己 | 血量／飽食／手上物品／準心指向數值化（F6） |
| `HtF.AmmoCounter` | `HtF_AmmoCounter` | `htf.ammocounter` | 只有你自己 | 手上槍械的剩餘子彈，數字／圓點（F10） |
| `HtF.Guardian` | `HtF_Guardian` | `htf.guardian` | 只有房主 | ServerRpc 驗證層、速率限制、封鎖名單、監控面板（F11） |
| `HtF.HostRules` | `HtF_HostRules` | `htf.hostrules` | 只有房主 | 無段式難度、規則開關、玩家數值、死亡不掉落背包、抽魚權重、保底、咬鉤時間 |
| `HtF.RadioMusic` | `HtF_RadioMusic` | `htf.radiomusic` | 只有你自己（同步播放則全員） | 收音機自訂音樂、一首歌一個頻率、自動接下一首、一起聽（F7／F8） |
| `HtF.ConfigMenu` | `HtF_ConfigMenu` | `htf.configmenu` | 只有你自己 | 遊戲內設定管理頁面（通用，F9） |
| `HtF.DazedTools` | `HtF_DazedTools` | `htf.dazedtools` | 只有你自己 | 遊戲內建 dev 指令的圖形介面（Insert） |

每個都是獨立的 DLL 與設定檔，可以單獨安裝，執行期彼此沒有相依。

## 環境

| 項目 | 版本 |
|---|---|
| 遊戲 | How to Fish 1.0.9（Unity 6000.4.4f1，Mono） |
| Mod loader | BepInEx 5.4.23.5，Harmony 2.9 |
| 目標框架 | netstandard2.1 |

## 建置

參照是對遊戲的 `Managed/*.dll` 做萬用字元展開，所以要指到你自己的安裝位置：

```bash
dotnet build mods/HtF.HudNumbers/HtF.HudNumbers.csproj \
  -p:GameManaged="D:\SteamLibrary\steamapps\common\How to Fish\How to Fish\How to Fish_Data\Managed" \
  -p:ProfileDir="%AppData%\r2modmanPlus-local\HowToFish\profiles\Default"
```

建置後會自動部署到 r2modman 的 profile。預設路徑寫在 `mods/Common.props`。

**「裝進自己的遊戲」和「打包」是兩件事，別搞混：**

| 想做什麼 | 指令 |
|---|---|
| 改完程式碼，進遊戲試 | `dotnet build ... -c Release`（**不要**加 `-p:PluginOut`） |
| 產生要上傳的 zip | `python .github/tools/make_packages.py` |
| 兩件事一起 | `python .github/tools/make_packages.py --deploy` |

打包腳本**刻意**把 `PluginOut` 導去暫存資料夾，讓打包沒有副作用。代價是它不會更新你的
profile——只跑打包的話，`dist/` 是新的，你實際載入的那份還是舊的。`--deploy` 就是為了
這個而存在。

遊戲開著時 DLL 會被鎖住，而 `Copy` 有 `ContinueOnError="true"`，會**靜靜跳過**。
所以建置前先關掉遊戲；`--deploy` 沒看到部署訊息時會留一則 warning。

## 發布到 Thunderstore

社群是 [how-to-fish](https://thunderstore.io/c/how-to-fish/)。七個 mod 各是一個獨立套件，
套件名把點換成底線（`HtF.HudNumbers` → `HtF_HudNumbers`）——Thunderstore 的 `name`
只收 `a-zA-Z0-9_`。

每個 mod 資料夾裡的這五個檔案，就是套件根目錄要放的東西：

| 檔案 | 用途 |
|---|---|
| `manifest.json` | 套件名、版本、說明、相依（`BepInEx-BepInExPack-5.4.2305`） |
| `README.md` | **英文**。Thunderstore 套件頁面顯示的就是它 |
| `README_ZH.md` | 同一份的繁體中文版，兩份互相連結 |
| `CHANGELOG.md` | 版本更新記錄 |
| `icon.png` | 256×256，由 `.github/tools/make_icons.py` 產生 |

zip 的版面：

```
HtF_HudNumbers.zip
├── manifest.json
├── README.md
├── CHANGELOG.md
├── icon.png
└── BepInEx/plugins/HtF_HudNumbers/HtF.HudNumbers.dll
```

### 圖示

七張 `icon.png` 由 [`.github/tools/make_icons.py`](.github/tools/make_icons.py) 產生
（需要 Pillow），改顏色或字符就重跑一次：

```bash
python .github/tools/make_icons.py
```

規則只有一條：**一個字符、白色、佔畫面一半**。Thunderstore 要求剛好 256×256，
但清單上真正被看到的大約是 48px——塞第二個元素或把字符放大填滿，縮下去就糊了。
底色帶功能：**冷色 = 只有你自己要裝，暖色 = 只有房主要裝**。

`check_repo.py` 會讀 PNG 的 IHDR 驗尺寸。尺寸錯了是上傳當下才被打回，
而且錯的是一個肉眼看不出來的數字，所以這一條是 error 不是提醒。

### 版本號只有一個來源

`<Version>` 寫在各 mod 的 `.csproj`，另外兩處都從它來、或跟它對帳：

| 落點 | 怎麼跟上 |
|---|---|
| `[BepInPlugin]` | `Common.props` 的 `GenerateModInfo` 產生 `ModInfo.Version`，屬性直接引用它 |
| `manifest.json` | 字面值，由 `.github/tools/check_repo.py` 對帳，不一致就 CI 紅燈 |

Thunderstore **不收重覆的版本號**——送錯了那個號碼就用掉了，只能再開一版。
所以這一條是硬檢查，不是提醒。

### 打包

```bash
python .github/tools/make_packages.py
```

產出在 `dist/`，一個 mod 一個 zip（`HtF_HostRules-1.1.0.zip`），外加一份 `RELEASE_NOTES.md`。
腳本**開頭會先跑 `check_repo.py`**：版本號對不上就不該包，理由同上。

| 參數 | 用途 |
|---|---|
| `--no-build` | 不重新建置，用 `bin/Release` 底下現成的 DLL |
| `--only HtF.Guardian` | 只重包一個。這時不會清掉 `dist/` 裡的其他 zip，也不會動 `RELEASE_NOTES.md` |
| `--deploy` | 打包完順便部署到本機的 r2modman profile（遊戲要先關掉） |
| `--game-managed` / `--bepinex-core` | 覆寫參照組件的路徑 |

打包不會順手裝進你的 r2modman——`PluginOut` 會被蓋掉，
`Common.props` 的 `DeployToProfile` 寫到 `dist/` 底下的暫存夾，結束時刪掉。

#### 改過原始碼就要重新打包

`dist/` 裡的 zip 是**某一次建置的快照**，不會自己跟著原始碼更新。改完程式碼直接重跑
`make_packages.py` 就好（它預設會重新建置）。

`--no-build` 有一道護欄：腳本會比對 DLL 與該 mod 所有輸入（自己的 `.cs`、`.csproj`、
共用的 `Shared/*.cs` 與 `Common.props`）之中最新的修改時間，DLL 比較舊就擋下來。
但那只擋得住「忘了重建」。

**版本號已經上架過就不能重包同一號。** Thunderstore 的版本號用掉就是用掉了。所以
改原始碼時先問一句：這個版本發布過了嗎？

| 情況 | 要做的事 |
|---|---|
| 還沒發布過 | 直接重跑 `make_packages.py` |
| 已經上架 | 先把 `.csproj` 的 `<Version>` 加一版、`manifest.json` 跟著改、`CHANGELOG.md` 補一條，再打包 |

`check_repo.py` 會確認 `<Version>` 和 `manifest.json` 對得上，但它**沒辦法知道哪個版本
已經送上 Thunderstore 了**——那一段只能靠這條規則。

### 發布

[`.github/workflows/release.yml`](.github/workflows/release.yml)：

| 觸發 | 做什麼 |
|---|---|
| 推一個 `v*` 的 tag | 建置、打包、**建立 Release 並附上七個 zip** |
| 手動 workflow_dispatch | 一樣建置打包，但只留成 artifact，不建 Release |

手動那條是給你先試跑用的：下載 artifact 看過沒問題再推 tag。

**tag 只是這個 repo 的發布標記，不是套件版本。** 七個套件各有自己的版本號，
一次發布裡它們通常是不一樣的，所以 tag 叫 `v2026.08.26` 或 `v3` 都行。

發布要用和 CI 的 `build` 一樣的參照組件設定（見下面「持續整合」），
**但這裡沒設定是直接失敗，不是跳過**——一個沒有任何 zip 的 Release
比一個紅掉的 workflow 難處理得多。

上傳時 `dist/` 裡的 zip 可以直接丟進 Thunderstore 的上傳頁面，不用解壓重包。

## 持續整合

`.github/workflows/ci.yml`，推上 `main` 和開 PR 時跑：

| Job | 要遊戲組件嗎 | 做什麼 |
|---|---|---|
| `checks` | 不要 | Roslyn 把 `mods/**/*.cs` 逐檔剖析（語法、編碼）、BepInPlugin 的 GUID 有沒有撞號、兩份 README 的表格和實際的專案對不對得上 |
| `build` | 要 | 以 Release 建置七個 mod，DLL 收成 artifact |

**`build` 預設是跳過的。** 完整編譯得對著遊戲的 `Managed/*.dll`，而那份東西不進版控
（理由見下面「不在這個 repo 裡的東西」），公開 repo 的 CI 也沒有別的地方拿得到它。
要打開它：

1. 開一個**私有** repo，根目錄放 `Managed/` 和 `BepInEx/core/`——就是你本機那兩個資料夾。
2. 在這個 repo 設 repository variable `GAME_LIBS_REPO` = `<owner>/<那個私有 repo>`。
3. 再設 repository secret `GAME_LIBS_TOKEN` = 讀得到它的 fine-grained PAT。

沒設定時 `build` 只留一則 notice 就跳過，不會把 PR 弄紅，`checks` 照跑。
從別人的 fork 送 PR 時拿不到 secret，行為一樣是跳過。

## 文件

- `mods/README.md` — 七個 mod 的設計說明、中英雙語架構、踩過的坑
- `mods/<專案>/README.md` — 該 mod 的套件說明（**英文**，Thunderstore 頁面用的就是它）
- `mods/<專案>/README_ZH.md` — 同一份的繁體中文版
- `mods/HtF.Guardian/README_ZH.md` — 反外掛驗證層：問題、做法、擋不住的東西
- `mods/HtF.DazedTools/README_ZH.md` — 指令工具視窗

## 不在這個 repo 裡的東西

遊戲本體與 FishNet 的**反編譯原始碼不進版控**——那是別人的程式碼，不該由這裡散布，
而且隨時能從 DLL 重新產生。
文件裡引用到的型別與方法名，都以你自己反編譯出來的那份為準。

## 授權

[MIT](LICENSE)。七個 mod 都適用。

遊戲本體與 FishNet 的程式碼**不在**這個授權的範圍內——那是別人的，這個 repo 也沒有散布它們。

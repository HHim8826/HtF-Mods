# HtF-Mods — How to Fish 的八個 BepInEx mod

八個各自獨立的 mod：HUD 數值化、剩餘子彈、反外掛驗證層、房主規則、
經濟與釣魚生態、收音機自訂音樂與同步收聽、遊戲內設定頁面，以及一個指令工具視窗。
**介面中英雙語**，跟著遊戲語系自動切換。

Eight standalone BepInEx mods for *How to Fish*: HUD numbers, an ammo counter, a
host-side anti-cheat layer, host rules, economy and fishing ecology, custom radio music,
an in-game settings page, and a command tool window.
**The UI is bilingual (繁體中文 / English)** and follows the game's own
language setting automatically.

| 專案 | GUID | 誰要裝 | 做什麼 |
|---|---|---|---|
| `HtF.HudNumbers` | `htf.hudnumbers` | 只有你自己 | 血量／飽食／手上物品／準心指向數值化（F6） |
| `HtF.AmmoCounter` | `htf.ammocounter` | 只有你自己 | 手上槍械的剩餘子彈，數字／圓點（F10） |
| `HtF.Guardian` | `htf.guardian` | 只有房主 | ServerRpc 驗證層、速率限制、封鎖名單、監控面板（F11） |
| `HtF.HostRules` | `htf.hostrules` | 只有房主 | 無段式難度、規則開關、玩家數值 |
| `HtF.Economy` | `htf.economy` | 房主（售價顯示要一致則全員） | 賣價、花費、起始金錢、抽魚權重、保底、咬鉤時間 |
| `HtF.RadioMusic` | `htf.radiomusic` | 只有你自己（同步播放則全員） | 收音機自訂音樂、一首歌一個頻率、自動接下一首、一起聽（F7／F8） |
| `HtF.ConfigMenu` | `htf.configmenu` | 只有你自己 | 遊戲內設定管理頁面（通用，F9） |
| `HtF.DazedTools` | `htf.dazedtools` | 只有你自己 | 遊戲內建 dev 指令的圖形介面（Insert） |

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

## 持續整合

`.github/workflows/ci.yml`，推上 `main` 和開 PR 時跑：

| Job | 要遊戲組件嗎 | 做什麼 |
|---|---|---|
| `checks` | 不要 | Roslyn 把 `mods/**/*.cs` 逐檔剖析（語法、編碼）、BepInPlugin 的 GUID 有沒有撞號、兩份 README 的表格和實際的專案對不對得上 |
| `build` | 要 | 以 Release 建置八個 mod，DLL 收成 artifact |

**`build` 預設是跳過的。** 完整編譯得對著遊戲的 `Managed/*.dll`，而那份東西不進版控
（理由見下面「不在這個 repo 裡的東西」），公開 repo 的 CI 也沒有別的地方拿得到它。
要打開它：

1. 開一個**私有** repo，根目錄放 `Managed/` 和 `BepInEx/core/`——就是你本機那兩個資料夾。
2. 在這個 repo 設 repository variable `GAME_LIBS_REPO` = `<owner>/<那個私有 repo>`。
3. 再設 repository secret `GAME_LIBS_TOKEN` = 讀得到它的 fine-grained PAT。

沒設定時 `build` 只留一則 notice 就跳過，不會把 PR 弄紅，`checks` 照跑。
從別人的 fork 送 PR 時拿不到 secret，行為一樣是跳過。

## 文件

- `mods/README.md` — 八個 mod 的設計說明、中英雙語架構、踩過的坑
- `mods/HtF.Guardian/README.md` — 反外掛驗證層：問題、做法、擋不住的東西
- `mods/HtF.DazedTools/README.md` — 指令工具視窗

## 不在這個 repo 裡的東西

遊戲本體與 FishNet 的**反編譯原始碼不進版控**——那是別人的程式碼，不該由這裡散布，
而且隨時能從 DLL 重新產生。
文件裡引用到的型別與方法名，都以你自己反編譯出來的那份為準。

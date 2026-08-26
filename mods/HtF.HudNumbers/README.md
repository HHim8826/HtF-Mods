# HtF HUD Numbers

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HudNumbers/README_ZH.md)

Puts real numbers on the *How to Fish* HUD: health, fullness, poison and fire, the money you are
carrying, the item in your hands, and whatever your crosshair is pointing at.

**Only you need it.** No Harmony patches, no packets — it reads values and draws them. Using it in
someone else's lobby changes nothing for anyone but you.

## What it shows

| Section | Contents |
|---|---|
| Health and fullness | Actual numbers for health, fullness, poison and fire |
| Money | How much you are carrying |
| Held item | Name, value, weight, cookedness, score multiplier |
| Crosshair target | Health and stats of the creature or item you are looking at |

Each of the four can be turned off on its own.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.HudNumbers.dll` into `BepInEx/plugins/`.

Press **F6** in game to show or hide the panel.

## Settings

`BepInEx/config/htf.hudnumbers.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

| Section | Setting | Default | |
|---|---|---|---|
| General | Enabled | on | Master switch |
| | Toggle Key | F6 | |
| | Hide While Paused | on | Also hides while typing, or whenever the game hides its own UI |
| Layout | Corner | Top Left | |
| | Horizontal Margin | 12 | Pixels from that corner |
| | Vertical Margin | 12 | |
| | Scale | 1.0 | 0.5 – 2.5 |
| | Font | Microsoft JhengHei UI | System font name. Empty = the built-in Unity font, which renders CJK as boxes |
| Contents | Health And Fullness | on | |
| | Money | on | |
| | Held Item | on | |
| | Crosshair Target | on | |
| | Crosshair Ray Distance | 60 | Metres, 5 – 300 |

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the table above are what the in-game settings page shows you.

## Language

The UI is bilingual. It follows the single language setting owned by
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu)
(Auto / 中文 / English). Without that mod installed it follows the game's own language, switching
live — no restart.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

## Links

- [Source, and the other six HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HudNumbers/CHANGELOG.md)
- MIT licensed

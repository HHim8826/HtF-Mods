# HtF Ammo Counter

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.AmmoCounter/README_ZH.md)

Shows how many rounds are left in the gun you are holding — as a number, a row of pips, or both.

**Only you need it.** No Harmony patches, no packets. It reads `Weapon.Ammo` and the magazine size
from the weapon's attachments and draws them. Using it in someone else's lobby changes nothing for
anyone but you.

There is no reserve-ammo number because the game has no reserve ammo: refilling fills the magazine
outright. So the display is only *rounds in the magazine / magazine size*, and the size follows the
extended-magazine attachment.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.AmmoCounter.dll` into `BepInEx/plugins/`.

Press **F10** in game to show or hide the counter.

## Settings

`BepInEx/config/htf.ammocounter.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

| Section | Setting | Default | |
|---|---|---|---|
| General | Enabled | on | Master switch |
| | Toggle Key | F10 | |
| | Hide While Paused | on | Also hides while typing, or whenever the game hides its own UI |
| | Hide While Aiming | off | Keeps it off the scope while aiming down sights |
| Layout | Position | Below Crosshair | Or any screen corner |
| | Horizontal Offset | 0 | Pixels |
| | Vertical Offset | 90 | |
| | Scale | 1.0 | 0.5 – 3.0 |
| | Font | Microsoft JhengHei UI | System font name. Empty = the built-in Unity font, which renders CJK as boxes |
| Display | Style | Numbers And Pips | Numbers / Pips / both |
| | Show Magazine Size | on | `7 / 8` instead of `7` |
| | Show Weapon Name | off | |
| | Reload Hint | on | Shown while the weapon is reloading |
| | Low Ammo Threshold | 0.34 | Warning colour below this fraction of a magazine. 0 = only when empty |
| | Blink When Empty | on | |

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

- [Source, and the other seven HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.AmmoCounter/CHANGELOG.md)
- MIT licensed

# HtF Config Menu

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.ConfigMenu/README_ZH.md)

An in-game settings page for your BepInEx mods, with a button added to the pause menu and the main
menu. Press **F9** to open it.

**It is not tied to the HtF mods.** It enumerates every loaded plugin through BepInEx's
`Chainloader.PluginInfos`, so any mod you install later shows up on its own, with no update needed
here. If a mod ships bilingual setting names, they are picked up and shown in your language;
otherwise you get the names the mod itself declared.

**Only you need it.** It reads and writes config files. It does not touch game state and does not
send packets. While the window is open your player input is blocked, so you do not wander off or
fire a shot behind it.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.ConfigMenu.dll` into `BepInEx/plugins/`.

## It owns the language setting

The other seven HtF mods have bilingual interfaces and they all read **this one setting**. Set
`Language` here and every one of them follows, live, without a restart. `Auto` follows whatever
language the game itself is set to.

Mods installed without this one fall back to the game's language on their own, so nothing breaks —
you just lose the manual override.

## Settings

`BepInEx/config/htf.configmenu.cfg`, written on first run.

| Section | Setting | Default | |
|---|---|---|---|
| Interface | Language | Auto | Auto / 中文 / English. Every HtF mod follows this |
| | Toggle Key | F9 | |
| | UI Scale | 1.0 | 0.6 – 2.0 |
| | Font | Microsoft JhengHei UI | System font name. Empty = the built-in Unity font, which renders CJK as boxes |
| | Show Raw Values | off | Shows the exact string each setting writes into its `.cfg`. For debugging |
| Menu Button | Show In Pause Menu | on | |
| | Show In Main Menu | on | |
| | Button Label | *empty* | Empty = follows the language setting. Type anything to pin it |
| | Button Position | Above the last button | Or top / bottom of the column |
| | Diagnostics: Dump Button Groups | off | Dumps every button in the scene, grouped by parent, into the BepInEx log. Turn this on if a game update breaks the injection |

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the table above are what the settings page shows you.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

The only patch it applies is a postfix on `Player.BlockInputs`, used to block input while the
window is open.

## Links

- [Source, and the other seven HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.ConfigMenu/CHANGELOG.md)
- MIT licensed

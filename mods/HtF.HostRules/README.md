# HtF Host Rules

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HostRules/README_ZH.md)

Turns the game's three difficulty presets into a set of dials, and adds host-side rules on top:
friendly fire, one-shot kills, hunger, regeneration, poison, fire, PvP damage and revive values.

**Only the host needs it.** Everything it changes is computed server-side — damage resolution and
the hunger / regen ticks. Installing it as a plain client does nothing at all, good or bad. Nobody
else in the lobby needs it.

Changes take effect immediately; you do not have to restart the lobby.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.HostRules.dll` into `BepInEx/plugins/`.

## Settings

`BepInEx/config/htf.hostrules.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

Multipliers act on the game's own defaults, which are listed so you can see what a value of 1
actually means.

### Difficulty Multipliers

| Setting | Default | |
|---|---|---|
| Override Difficulty Multipliers | off | When on, the two below replace the game's Easy / Default / Hard presets |
| Creature Health Multiplier | 1.0 | Applied to `Creature.MaxHp`. Vanilla: Easy 0.75, Default 1, Hard 1.25 |
| Player Damage Taken Multiplier | 1.0 | All damage players take. Vanilla: Easy 0.5, Default 1, Hard 1.25 |

### Rules

| Setting | Default | |
|---|---|---|
| Friendly Fire | Unchanged | Force the lobby's friendly fire toggle on or off |
| One-Shot Kills | Unchanged | When forced on, melee, fists and bullets all deal a flat 99999 |

### Player Stats

| Setting | Default | Vanilla behaviour it scales |
|---|---|---|
| Hunger Speed Multiplier | 1.0 | 1 fullness lost every 300 ticks |
| Starvation Damage Multiplier | 1.0 | 5 damage every 150 ticks once fullness hits zero |
| Regen Speed Multiplier | 1.0 | Heals once every 100 ticks |
| Regen Amount Multiplier | 1.0 | 5 health per heal |
| Poison Damage Multiplier | 1.0 | 5 every 100 ticks |
| Fire Damage Multiplier | 1.0 | 10 every 50 ticks |
| Player vs Player Damage Multiplier | −1 | Absolute override, −1 = leave alone. Vanilla is 0.25 |
| Health On Revive | −1 | Absolute override, −1 = leave alone. Vanilla is 25 |
| Fullness On Revive | −1 | Absolute override, −1 = leave alone. Vanilla is 10 |
| Invulnerability After Damage | −1 | Seconds. −1 = leave alone. Vanilla is 0.25 |

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the tables above are what the in-game settings page shows you.

## What it deliberately does not do

It adds no new synchronised state. FishNet's `SyncVar`s are produced by compile-time IL weaving,
and Harmony cannot add one at runtime — so this mod only changes results the server already
computes. That is also why it is host-only rather than something clients could opt into.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

The difficulty dials hook the static multipliers on `ServerSettings`. Patch targets are verified at
startup, so a game update that moves them produces a warning in the BepInEx log rather than silent
no-ops.

## Links

- [Source, and the other seven HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HostRules/CHANGELOG.md)
- MIT licensed

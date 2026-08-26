# HtF Host Rules

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.HostRules/README_ZH.md)

Turns the game's three difficulty presets into a set of dials, and adds host-side rules on top:
friendly fire, one-shot kills, hunger, regeneration, poison, fire, PvP damage, revive values — and
the fishing ecology: catch weights, per-species multipliers, pity rolls and bite time.

**Only the host needs it.** Everything it changes is computed server-side — damage resolution, the
hunger / regen ticks, and the catch roll. Installing it as a plain client does nothing at all, good
or bad. Nobody else in the lobby needs it.

Changes take effect immediately; you do not have to restart the lobby.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.HostRules.dll` into `BepInEx/plugins/`.

## Upgrading from HtF.Economy

The fishing half of `HtF.Economy` lives here now. It was never really *economy*: catch weights,
pity and bite time are rules the host sets for the whole lobby, and they take effect in exactly the
same place as the difficulty multipliers — so having them in a second mod only meant opening two
settings pages to tune one thing.

**If `HtF.Economy` or `HtF.FishingEcology` is still installed, delete it.** All three patch
`CreatureManager.GetRandomItem` the same way, so two of them loaded at once make the multipliers
compound, with no error to tell you. This mod writes a warning to the BepInEx log if it spots one.

`HtF.Economy`'s money half — sell price multiplier, cost multiplier, starting money for new saves —
was **removed**, not moved. If you were using those, this release drops them.

Your old `htf.economy.cfg` does not carry over: the plugin GUID is different, so BepInEx writes a
fresh file. The setting names are unchanged, so they are quick to re-enter.

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

### Fishing

| Setting | Default | |
|---|---|---|
| Enable Fishing Ecology | on | Off = vanilla catch weights. Does not affect anything above |
| Rare Threshold | 0.25 | Anything at or below (largest weight in that bait's table × this) counts as rare |
| Rare Multiplier | 1.0 | Above 1 = rare fish show up more often |
| Common Multiplier | 1.0 | Lowering this raises rare fish relative to everything else |
| Boss Multiplier | 1.0 | Weight of boss creatures. The game's own "only one boss alive" rule is untouched |
| Per-Item Multipliers | *empty* | `name=multiplier`, comma separated, lowercase with spaces removed — e.g. `tuna=5, giantpiranha=0.2`. Overrides the three above |
| Pity After N Non-Rare Rolls | 0 | 0 = off. After N non-rare rolls in a row, the next draws only from the rare items. The counter is shared by the whole lobby |
| Bite Time Multiplier | 1.0 | Below 1 = fish bite sooner. Edits the `BaitInfo` asset, restored when you quit |

### Debug

| Setting | Default | |
|---|---|---|
| Log Every Roll | off | Every catch roll into the BepInEx log. Handy while tuning |

#### How rarity is decided

The game has no rarity field for fish — `Rarity` is only used for skins. So rarity is derived from
the weights themselves: within one bait's table, the harder something is to roll, the rarer it is.
That is what `Rare Threshold` compares against.

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

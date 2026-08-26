# HtF Economy

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.Economy/README_ZH.md)

Retunes the money and the fish: sell prices, what things cost, starting money for new saves, catch
weights, per-species multipliers, a pity counter, and how long fish take to bite.

The two halves used to be separate mods. They are the same curve seen from both ends — **fish are
where money comes from** — and retuning one without the other reliably produces something lopsided.

## Who installs it

**The host.** Everything here is settled server-side.

**Everyone, if you want displayed prices to match.** The sell *value* shown in the UI is computed on
each client. With only the host running the mod, everyone still receives the adjusted money; their
UI just shows the unmodified number.

**Bite time and catch weights are host-only** in the strict sense: the only place they are read runs
server-side, so tuning them on a pure client does nothing at all.

## Upgrading from HtF.FishingEcology

That mod is now part of this one. **If it is still installed, remove it.** With both loaded the catch
weights get patched twice and the multipliers compound, with no error to tell you. This mod writes a
warning to the BepInEx log if it sees the old plugin.

Delete the `BepInEx/plugins/HtF.FishingEcology` folder, or uninstall the package in your mod manager.

Your existing `htf.economy.cfg` carries over untouched — the money settings kept their original
section and key names on purpose.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.Economy.dll` into `BepInEx/plugins/`.

## Settings

`BepInEx/config/htf.economy.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

### Multipliers

| Setting | Default | |
|---|---|---|
| Sell Price Multiplier | 1.0 | Every item's value (`Item.TotalWorth`). Below 1 = hardcore, above 1 = relaxed. Affects the sell box, NPC buyers, and the price shown in the UI |
| Cost Multiplier | 1.0 | What purchases actually deduct — shop, bait, pockets, attachments, upgrades, bets. **The shop UI still shows the original price, and so does the can-you-afford-it check**; only the amount deducted is scaled, clamped at 0 |

### Save Files

| Setting | Default | |
|---|---|---|
| Starting Money For New Saves | −1 | −1 = leave alone (the game gives 0). Only applies to saves created from now on |

### Fishing

| Setting | Default | |
|---|---|---|
| Enable Fishing Ecology | on | Off = vanilla catch weights. Does not affect the multipliers above |
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
| Log Money Changes | off | Every deduction into the BepInEx log |
| Log Every Roll | off | Every catch roll into the BepInEx log. Handy while tuning |

### How rarity is decided

The game has no rarity field for fish — `Rarity` is only used for skins. So rarity is derived from
the weights themselves: within one bait's table, the harder something is to roll, the rarer it is.
That is what `Rare Threshold` compares against.

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the tables above are what the in-game settings page shows you.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

## Links

- [Source, and the other seven HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.Economy/CHANGELOG.md)
- MIT licensed

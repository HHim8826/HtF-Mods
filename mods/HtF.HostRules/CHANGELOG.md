# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).

## 1.2.0

Two new settings under `Death`, both **off by default** - they remove one of the game's core
penalties, so the host has to ask for them.

- **Keep Inventory On Death.** Giving up and respawning no longer drops your whole inventory and
  the item in your hands. Note the vanilla rule this switches off: when everyone is down, *every*
  player drops, not just the one who gave up. The deliberate drop-all command in `HtF_DazedTools`
  still works - only the drop that respawning causes is suppressed.
- **Keep Held Item When Downed.** When you go down into the revivable body on the ground, the item
  in your hands is put into your inventory instead of dropping. If the inventory is full it still
  drops, because there is nowhere to put it.

  It goes into the inventory rather than staying in your hands because staying in your hands is not
  possible from the host side: the downed player's own client plays the drop locally, and no host
  can reach that. Putting it in the inventory makes that same client code stow it instead.

## 1.1.0

- Absorbed the fishing half of `HtF.Economy`: catch weights (rare / common / boss multipliers,
  rare threshold), per-item multipliers, the pity counter, and the bite time multiplier.
  They were never really "economy" — they are rules the host sets for the whole lobby, and they
  take effect in exactly the same place as the difficulty multipliers already here.
- **`HtF.Economy` is gone.** Its money half — sell price multiplier, cost multiplier and starting
  money for new saves — was **removed**, not moved.
- **If `HtF.Economy` or `HtF.FishingEcology` is still installed, delete it.** All three patch
  `CreatureManager.GetRandomItem` the same way, so two of them loaded at once make the multipliers
  compound, with no error to tell you.
  When one is detected this mod **disables its own fishing half** and says so in the log, so the
  multipliers cannot compound while you sort it out. Difficulty, rules and player stats keep working.
- Fishing settings do not carry over from `htf.economy.cfg` — the plugin GUID is different, so
  BepInEx writes a fresh file. The setting names are unchanged, so they are quick to re-enter.
- Fixed: turning `Enable Fishing Ecology` off left the bite time multiplier written into the
  `BaitInfo` asset, and re-applied it on the way out. Fish kept biting faster until you quit the
  game. The toggle now restores the vanilla values.
- Fixed: when the weight table could not be built (empty table, field names not matching, all
  weights zero), the pity counter still compared the result against the *previous* roll's rare set,
  so rare detection and the pity streak were both wrong. Those rolls are now skipped.
- Changing any setting no longer rewrites every bait asset — only the two settings that actually
  touch it do.

## 1.0.0

Initial release.

- Stepless difficulty: creature health and player damage taken replace the game's
  Easy / Default / Hard presets.
- Force friendly fire and one-shot kills on or off.
- Player stat multipliers: hunger speed, starvation damage, regen speed and amount,
  poison damage, fire damage, player-versus-player damage.
- Absolute overrides for health and fullness on revive, and for invulnerability after damage.
- Changes apply immediately; no need to restart the lobby.
- Bilingual UI, following the language setting of `HtF_ConfigMenu` or the game's own language.

# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).

## 1.1.0

- Absorbed the fishing half of `HtF.Economy`: catch weights (rare / common / boss multipliers,
  rare threshold), per-item multipliers, the pity counter, and the bite time multiplier.
  They were never really "economy" — they are rules the host sets for the whole lobby, and they
  take effect in exactly the same place as the difficulty multipliers already here.
- **`HtF.Economy` is gone.** Its money half — sell price multiplier, cost multiplier and starting
  money for new saves — was **removed**, not moved.
- **If `HtF.Economy` or `HtF.FishingEcology` is still installed, delete it.** All three patch
  `CreatureManager.GetRandomItem` the same way, so two of them loaded at once make the multipliers
  compound, with no error to tell you. This mod writes a warning to the BepInEx log if it sees one.
- Fishing settings do not carry over from `htf.economy.cfg` — the plugin GUID is different, so
  BepInEx writes a fresh file. The setting names are unchanged, so they are quick to re-enter.

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

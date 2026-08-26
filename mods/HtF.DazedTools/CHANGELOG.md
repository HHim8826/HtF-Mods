# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).


## 1.0.0

Initial release.

- A window (Insert by default) over the game's own built-in dev commands, generated from a command
  registry: categories on the left, typed parameter controls per command.
- Searchable list of all 85 items, current-player dropdowns, and bait / pocket pickers that skip
  the indices that make the server throw.
- Console at the bottom with command history; chat-bar `/commands` keep working.
- Danger lock: destructive commands stay disabled until you open it.
- Optional switch for the game's own cheat hotkeys (`ClientSettings.CheatsEnabled`).
- Configurable toggle key, window scale and system font.
- Bilingual UI, following the language setting of `HtF_ConfigMenu` or the game's own language.

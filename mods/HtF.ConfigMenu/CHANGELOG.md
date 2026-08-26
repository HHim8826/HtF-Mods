# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).


## 1.0.0

Initial release.

- Lists the settings of every loaded BepInEx plugin, discovered through `Chainloader.PluginInfos`,
  so mods installed later show up without an update here.
- Buttons injected into the pause menu and the main menu, with a configurable label and position.
- Owns the language setting the other HtF mods follow (Auto / 中文 / English).
- Configurable toggle key (F9 by default), window scale and system font.
- Optional raw-value display and a diagnostic dump of the menu button groups.
- Blocks player input while the window is open.

### Upgrading from an earlier build

If you ran a pre-release build of this mod, its `htf.configmenu.cfg` already contains
`模組設定` as the button label. That value is now treated as empty, meaning the button follows
the language setting. Type any other label to pin it.

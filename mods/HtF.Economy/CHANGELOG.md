# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).


## 1.1.0

- Merged the separate `HtF.FishingEcology` mod into this one. They were two ends of the same
  curve — fish are where money comes from — and both take effect in the same place, on the host.
  Catch weights, pity rolls and bite time now live under the Fishing section here.
- **If you still have `HtF.FishingEcology` installed, remove it.** Both loaded at once patch the
  catch weights twice and the multipliers compound. This mod warns about it in the BepInEx log.
- The money section keeps its original section and key names, so an existing `htf.economy.cfg`
  carries over untouched.
- Fixed: starting money for new saves was applied in memory but not written back to disk, so a
  crash before the next save dropped it back to 0.

## 1.0.0

Initial release.

- Sell price multiplier, applied to every item's value.
- Cost multiplier, applied to what purchases actually deduct.
- Starting money for new saves.
- Optional logging of every money change.

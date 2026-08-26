# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).


## 1.0.0

Initial release.

- Guards on all 56 of the game's `[ServerRpc(RequireOwnership = false)]` entry points, each checked
  against the connection FishNet says actually sent the packet.
- Actor identity, purchase prices, value ranges and index ranges, each switchable on its own.
- Per-connection, per-RPC token buckets. Rate drops are counted separately from violations and never
  contribute to kicks.
- Violation counter with time decay, and a configurable action once the limit is reached:
  log only, kick, or kick and ban.
- Ban list keyed on the Steam ID reported by the transport, applied when a connection is established.
- Monitor panel (F11 by default) with connections, recent blocked events and the ban list.
- Optional movement speed check, off by default.
- Bilingual UI, following the language setting of `HtF_ConfigMenu` or the game's own language.

# Changelog

All notable changes to this package. Versions follow [semantic versioning](https://semver.org/).


## 1.0.0

Initial release.

- Plays `.ogg` / `.wav` / `.mp3` from `BepInEx/config/HtF.RadioMusic` on the in-game radio.
  Loose files are handed out to the stations in turn; numbered subfolders pin tracks to a station.
- Station count: leave the game's own stations alone, set a number, or let it follow the track
  count so every song gets its own frequency.
- Station width, so more stations than vanilla can fit in the 88-108 band without bleeding into
  each other.
- Auto-advance: a station with several tracks plays them back to back like a real station.
- Listen Together: everyone in the lobby hears the same track at the same position, derived from
  FishNet's network time. No packets are sent; it requires everyone to have the same files.
- Adjustable static noise and music volume, and an option to keep playing through boss fights.
- Next track (F7) and reload music (F8) hotkeys.
- Bilingual UI, following the language setting of `HtF_ConfigMenu` or the game's own language.

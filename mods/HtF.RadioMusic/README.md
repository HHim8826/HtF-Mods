# HtF Radio Music

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.RadioMusic/README_ZH.md)

Puts your own music on the in-game radio — one song per frequency if you want, playing back to back
like a real station, with an optional mode that keeps the whole lobby on the same track.

**Only you need it.** The frequency is a `SyncVar`, so other people can see you tuning the dial, but
the audio files are local: your custom music is heard by you alone. **Listen Together is the
exception** — see below.

## Where the music goes

```
BepInEx/config/HtF.RadioMusic/
├── song.ogg          ← loose files are handed out to the stations in turn
├── another.ogg
├── 1/                ← anything in here is pinned to station 1
│   └── ...
└── 2/                ← station 2, and so on
```

The folder and a short text file explaining this are created for you on first run.

`.ogg`, `.wav` and `.mp3` are supported. **`.ogg` is the safest** — Unity decodes it most reliably at
runtime. `.mp3` does not load in every environment; failures are reported in the BepInEx log.

Press **F8** to pick up files you just added, without restarting the game.

## One song per frequency

Set `Station Count` to `0` (the default) and it follows your track count: every song gets its own
frequency, capped at however many fit in the 88–108 band.

Fitting more than vanilla's seven stations in that band takes `Station Width`. Vanilla stations are
full volume within 0.5 of centre and only fall silent past 1.5, so two of them need 3.0 between them
to stop bleeding into each other. Narrow them and the clean spacing narrows with them — any number
of stations separates cleanly. The cost is that tuning gets finer, which is what a real FM dial feels
like anyway. `0` picks a width from the spacing automatically.

## Listen Together

Turn on `Listen Together` and everyone in the lobby hears the same track at the same position.

Each station's tracks form one continuous timeline, like a station's schedule, and FishNet's network
time decides where playback currently is. It is pure computation — **no packets are sent**.

Two conditions, and the mod cannot enforce either:

- everyone has to install this mod, and
- everyone's music folder has to hold the same files. The mod cannot hand out audio for you.

While it is on, `Next Track` does nothing. Skipping is a local action and cannot be shared.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.RadioMusic.dll` into `BepInEx/plugins/`.

## Settings

`BepInEx/config/htf.radiomusic.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

| Section | Setting | Default | |
|---|---|---|---|
| Audio | Static Noise Volume | 1.0 | The off-station hiss. 0 = silence it. 0 – 3 |
| | Music Volume | 1.0 | Station volume. The vanilla baseline is 0.3. 0 – 5 |
| | Keep Playing During Boss | off | Vanilla mutes the whole radio when a boss shows up |
| | Auto-Advance Playlist | on | A station with several tracks plays them back to back |
| | Listen Together | off | See above |
| Stations | Station Count | 0 (automatic) | −1 = leave the game's stations alone. 0 = one per track. Any other number sets it. −1 – 20 |
| | Station Width | 0 (automatic) | 1 = the game's own width. 0 – 2 |
| Keys | Next Track | F7 | |
| | Reload Music | F8 | |

That hiss is deliberate, by the way — the game derives it from how far off-station you are, to
imitate FM. `Static Noise Volume` just scales it.

### When Next Track does nothing

With `Station Count` on automatic, each station holds exactly one song, so there is no next track to
skip to. For `Next Track` and `Auto-Advance` to mean anything you need fewer stations than tracks —
`−1`, the game's own stations, is the easy way.

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the table above are what the in-game settings page shows you.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

Patch targets are verified at startup, so a game update that moves them produces a warning in the
BepInEx log rather than silent no-ops.

## Links

- [Source, and the other six HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.RadioMusic/CHANGELOG.md)
- MIT licensed

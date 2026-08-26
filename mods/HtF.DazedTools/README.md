# HtF Dazed Tools

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.DazedTools/README_ZH.md)

A window over the game's own built-in dev commands. Press **Insert**, pick a category on the left,
fill in the parameters, press Run.

**Only you need it.** The commands it sends are the game's own `DazedCommands`; whether they take
effect is up to the lobby you are in — see "Using it with HtF Guardian" below.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.DazedTools.dll` into `BepInEx/plugins/`.

## Using it

1. Start the game modded.
2. Press **Insert**.
3. Pick a category, fill the parameters, press Run.

There is a console at the bottom you can type into directly (the leading `/` is optional), with
history on the up/down buttons. Typing `/xxx` in the chat bar still works too.

### The danger lock

Commands marked DANGER (`finishgame`, `sendfinishgame`, the achievement ones, `spoofprojectile`)
cannot be pressed until you open the lock at the top of the window.

## Why a plugin instead of edited source

The decompiled `Assembly-CSharp` **cannot be compiled as a whole** — FishNet's IL weaving produces
identifiers containing `.` and `-`, which are not legal C# — so edits to the decompiled source can
never become something you can hand to anyone. Harmony patching sidesteps that completely: it only
*references* `Assembly-CSharp.dll`, it does not rebuild it.

## Structure

| File | Role |
|---|---|
| `src/Plugin.cs` | BepInEx entry point, settings, hotkey, Harmony bootstrap |
| `src/Patches.cs` | 3 Harmony patches (below) |
| `src/Commands/CommandCore.cs` | The fixed command implementations, ported from `DazedCommands.cs` |
| `src/UI/CommandRegistry.cs` | Command metadata — **the entire UI is generated from it** |
| `src/UI/GameData.cs` | Lookup tables for bait, attachments, pockets, motors, NPCs, roulette |
| `src/UI/GameData.Items.cs` | All 85 items |
| `src/UI/Theme.cs` | Dark theme (textures generated at runtime, plus a copied GUISkin) |
| `src/UI/ModWindow.cs` | The IMGUI window |

### How the port was done

`CommandCore.cs` is a mechanical conversion of the decompiled `DazedCommands.cs`: decompiler token
comments stripped, `MonoBehaviour` turned into a static class, renames to avoid clashing with game
types, and the `ClientSettings.CheatsEnabled` gate removed (the plugin controls that itself).
**The command logic and every safety limit are unchanged** — batch cap 48, projectile cap 64, search
radius 60 m, the lower-bound checks in `/forceunlockpocket` and `/buybaitfree`, the NPC 255 block, the
casino singleton check.

Review afterwards added a few more guards that **the original had and this did not**:

| Where | Problem |
|---|---|
| `/spawndead` | Picking something that is not a creature (a radio, a gun) throws; the upstream version already checked |
| `/allskins` | Throws on an island with no boat — and `LockAllSkins()` has already wiped the whole skin list by then |
| `GetBatchItems` | Labelled 60 m but had no distance test, plus `Dictionary` order is undefined and multi-collider items were counted twice |
| `GetNearestItem` | The message claimed a 60 m limit; there wasn't one |
| `Build` (UI) | A blank middle parameter with no default ate its whole cell and shifted everything after it one to the left |

### Harmony patches

| Target | Kind | Purpose |
|---|---|---|
| `DazedCommands.IsServerCommand` | Prefix | Route chat-bar commands to the fixed implementations, skipping the original |
| `Player.BlockInputs` (getter) | Postfix | Block all local player input while the window is open |
| `ChatManager.ChatMessage(string)` | Postfix | Mirror the game's output into the window's output pane |

All three targets are named with `nameof`, so a typo fails at compile time instead of at runtime.

"Enable the game's own cheat keys" is **not** a patch. It used to be a postfix on the
`ClientSettings.CheatsEnabled` getter, which does not work: a one-line auto-property gets inlined
into its callers by Mono and the patch silently does nothing. It now calls the public
`ClientSettings.ToggleCheats(bool)`, comparing once per frame so it wins over the game's own cheat
button, and turning it back off only if this mod was what turned it on.

### The IMGUI structural-consistency rule (learned the hard way)

Unity runs `OnGUI` **several times per frame**: `Layout` first to compute the layout, then the mouse
and keyboard events, then `Repaint`. Changing the **layout structure** (control count, group nesting,
whether a block is shown) during one of the later passes disagrees with the tree computed on
`Layout`, and GUILayout hits a NullReferenceException — with a stack pointing inside whatever drawing
function you happened to be in, which reads like a bug in that function.

So `ModWindow`'s rule is: **any state change that alters structure is `Defer()`red and applied only on
the Layout event.** The known cases:

| Action | Why it is structural |
|---|---|
| Opening/closing a picker overlay | The whole window swaps to different content |
| Switching the left-hand tab | The command list is replaced wholesale |
| Typing in the picker's search box | The number of list entries changes |
| `PosOrVoid` switching mode | "Coordinates" mode adds three more input fields |
| Adding or clearing output messages | The number of labels changes |

Parameter rows also **wrap automatically** against a width budget, and the Run button always gets its
own row. Commands with several parameters (`/hitplayer`: player dropdown + damage + bypass PvP) add
up past the card width, and GUILayout pushes whatever is last out of the visible area — which is
exactly how the Run button used to disappear entirely.

The output list and the picker's filtered results are both snapshotted on `Layout` (`_logView`,
`PickerView`) and reused by the other events, so structure cannot change mid-frame. `PosOrVoid`'s mode
lives in its own `PosModes` rather than being inferred from the parameter string — otherwise clearing
the coordinates would flip `hasPos` halfway through a frame.

## Guards on the game's side (why some commands look broken)

All of these are **the game's own limits**, not a broken mod. Each has been traced in the source and
is called out in red in the UI.

| What you see | The actual reason |
|---|---|
| `/setitemmultiplier` does nothing on an item that already has one | `Item.SetKillscoreMultiplier`'s second guard is `if (_killScoreMultiplier.Value != 1f) return;` — an item can only be set once |
| `/setitemholder` cannot take something out of someone's hands | `RpcLogic___SetItemHolder` contains `if (syncedHolder && syncedHolder != A_2) { TargetReconcileRejectedItemPickup(...); return; }`. What it can actually do is put an **unheld** item into someone's hands |
| `/tpitems` does nothing at all while hosting | `RigidbodySync.ServerSetPosRot`'s entire body is wrapped in `if (netCon != InstanceFinder.ClientManager.Connection)`. That is echo suppression: the normal flow sends your own connection. As the host the condition is always false, so **the whole thing is skipped** |
| `/hijackitemphysics` has no visible effect | It only changes `_syncedSimulator`; there was never anything to see. And as the host, sending it to yourself changes nothing |
| `/forceplacebet` on green does not make green come up | `ServerStartBet(chosenColor)` sets `_curBetColor` — **the colour you bet on**, not the result. The spin is still random |
| `/spoofroulette` has no real effect | `CasinoManager.UpdateGameObjects(pos, angle)` only broadcasts where the roulette objects sit. Pure visual deception; it does not affect the result or the payout |
| `/steerboat` does nothing | `Boat.ServerSetInput` opens with `if (!IsServerInitialized \|\| !_driver.Value) return;` — no driver, whole thing ignored. And the real driver sends input every frame (`Boat.cs:721`), so a single shot is overwritten on the next one |

### What was changed because of that

- **`/tpitems` and `/hijackitemphysics` no longer send RPCs.** They call the public
  `RigidbodySync.StartSimulateLocal(pos, rot)` to take over simulation and then place the item —
  which is how the game itself moves things, and it works both as host and as client. Items already
  simulated locally go through `TeleportToPosRot()` instead, because `StartSimulateLocal` returns
  early in that case without moving anything (and it also skips placement when `pos` happens to equal
  `Vector3.zero`).
- **`/setitemholder` now defaults to the nearest unheld item**, and says up front that the server will
  reject the request if the target is already held.
- **`/steerboat` gained a duration parameter** (3 seconds by default), resent every frame by
  `PumpBoatInput()` so it overrides the real driver.
- **`/addmoney` and `/removemoney` take an amount** (still 9999 by default).
- **`/spoofchat` was removed.** It does work (`SendChatMessage` is an ObserversRpc with no
  `ExcludeServer`, so even the host receives it), but it is purely a tool for impersonating people in
  chat and has no debugging value.

## The UI is data-driven

Every command in `CommandRegistry.All` declares its own category, risk level and parameter types.
`ModWindow` has a single generic render loop — adding a command means adding one line to the
registry, with no UI code to touch. Parameter type decides the control: `Item` gets the searchable
85-entry list, `Player` gets a dropdown of the current players, and `Bait` / `Pocket` automatically
skip the index 0 that would get you kicked.

## Settings

`BepInEx/config/htf.dazedtools.cfg`, written on first run. Everything is also editable in game
through [HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

| Setting | Default | |
|---|---|---|
| Toggle Key | Insert | |
| UI Scale | 1.0 | 0.6 – 2.0 |
| Font | Microsoft JhengHei UI | System font name. Empty = the built-in Unity font, which renders CJK as boxes |
| Enable The Game's Own Cheat Keys | off | Turns on the game's debug hotkeys: M/N for money, O to change island, comma to skip the tutorial |
| Unlock Danger Commands By Default | off | |

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the table above are what the in-game settings page shows you.

## Language

The whole window is bilingual — categories, command descriptions, warnings, parameter names and
dropdown entries. It follows the language setting owned by
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu); without that
mod it follows the game's own language.

**The command strings it sends are not affected by language** — `/spawn tuna`, `me`, `all` and
`confirm` are the game's command syntax, not text for people to read.

## Using it with HtF Guardian

[HtF Guardian](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.Guardian) blocks exactly the
RPCs this mod sends. **They do not conflict while you are the host** — the host is exempt by default,
so the commands still work. Use this in someone else's lobby while they run Guardian and the commands
get blocked, which is the intended behaviour.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

## Building

```bash
dotnet build mods/HtF.DazedTools/HtF.DazedTools.csproj
```

Paths can be overridden with `-p:GameManaged="..."` / `-p:ProfileDir="..."`; the defaults live in
`../Common.props`.

## Not yet verified in game

IMGUI layering, font loading and `PlayerCamera.ToggleMouse` were all confirmed fine. What compiles
but has **not** been run yet is the batch of review fixes:

- `/allskins` on an island with no boat (should unlock item skins only and report one line, without
  touching boat skins)
- The 60 m radius and "nearest 48" ordering of `/hitcreature all`
- The message from `/spawndead` when the pick is not a creature
- The refusal message when a middle parameter is left blank (`/slots` with a blank item name is the
  easiest test)
- Whether the M/N/O hotkeys actually respond now that "enable the game's own cheat keys" calls
  `ToggleCheats` — this one was **entirely ineffective** before (it patched a getter that gets
  inlined), so it is effectively a first run

## Links

- [Source, and the other six HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.DazedTools/CHANGELOG.md)
- MIT licensed

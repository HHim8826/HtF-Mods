# HtF Guardian

English | [繁體中文](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.Guardian/README_ZH.md)

A host-side ServerRpc validation layer. It recovers the one piece of information the game throws
away — *which connection actually sent this packet* — and uses it to put an authorisation check and
a rate limit on each of the 56 `[ServerRpc(RequireOwnership = false)]` entry points.

**Only the host needs it.** Every `RpcLogic___*` runs server-side only, so on a pure client this mod
never executes anything.

## Install

**Mod manager (recommended).** Install through Thunderstore Mod Manager or r2modman and start the
game modded. BepInEx comes along as a dependency.

**Manual.** Install [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)
first, then drop `HtF.Guardian.dll` into `BepInEx/plugins/`.

Press **F11** while hosting to open the monitor panel. Enforcement starts on **Log Only** — see
Settings below.

## The problem

The game is host-authoritative (a listen server). `Server.cs` holds 56
`[ServerRpc(RequireOwnership = false)]` methods — `RequireOwnership = false` switches off FishNet's
automatic owner check, and the game **never replaced it with one of its own**.

FishNet actually delivers the answer to the door. It injects the real sending connection into every
ServerRpc reader:

```csharp
private void RpcReader___HitCreature___215526726(PooledReader r, Channel channel, NetworkConnection conn)
{
    Creature creature = ...; Player player = ...; int damage = ...;
    if (!base.IsServerInitialized) return;
    this.RpcLogic___HitCreature___215526726(creature, player, damage, hitPoint, dir);   // conn is never passed on
}
```

Of those 56 readers, **only `SpawnPlayer` passes `conn` down** (it uses it to check Steam lobby
membership). The other 55 drop it, which leaves the server trusting the `Player` / `SteamID` /
`cost` the client filled into the parameters to decide *who is acting, on whom, and for how much*.

The result: anyone who connects can deal arbitrary damage to any player, teleport any player off
the map, empty any player's inventory, speak as someone else, take items for `cost = 0`, place bets
on the whole team's behalf, and force the run to end.

## How it works

```
RpcReader___X(reader, channel, conn)
        |
        |  (1) prefix: Sender.Begin(conn)          <- remember who sent this
        v
   (parameters are read)
        |
        v
RpcLogic___X(a, b, c)
        |
        |  (2) prefix: validate a/b/c against Sender.Current
        |      invalid  -> return false (the original never runs)
        |      fixable  -> rewrite the ref parameter and let it through
        v
   (the original logic)
        |
        |  (3) reader finalizer: Sender.End()
```

| File | Role |
|---|---|
| `src/Plugin.cs` | BepInEx entry point, settings, hotkey |
| `src/Patcher.cs` | Finds the targets and applies the patches; prints coverage at startup |
| `src/Sender.cs` | "who sent the RPC we are in", plus connection to Player to SteamID |
| `src/Guards.cs` | The 56 guards themselves |
| `src/Report.cs` | Records violations, enforces (kick / ban), feeds the panel |
| `src/Limiter.cs` | Token bucket per connection per RPC |
| `src/Prices.cs` | Reads real prices off the shop objects in the scene |
| `src/Damagers.cs` | "who recently hit this creature", used only by `SetItemMultiplier` |
| `src/Speed.cs` | Movement speed check (off by default) |
| `src/Bans.cs` | Ban list file |
| `src/Watcher.cs` | Connection events; this is where the ban list takes effect |
| `src/Panel.cs` `src/Styles.cs` | The F11 monitor panel |

### Targets are found by prefix, never by a hardcoded name

The weaver-generated method is called `RpcLogic___HitCreature___215526726`, and that trailing number
is a signature hash — it changes the moment the game changes a parameter. So the search is on the
prefix `"RpcLogic___HitCreature___"`, and if a target genuinely disappears the mod says so at
startup. **It never fails silently.**

The startup log prints coverage:

```
Guarding 56 / 56 RPCs, 56 readers.
```

When a game update adds a new ServerRpc that denominator grows, and an "unguarded: ..." line appears
next to it.

### Parameters bind by position (`__0`, `__1`), not by name

**This one is a hard rule.** The parameters of `RpcLogic___*` **have no names in the metadata** — the
`A_1`, `A_2` you see in a decompiler are dnSpy's filler for unnamed parameters — so binding by name
is guaranteed to fail. Amusingly, the readers produced by the same weaver **do** have names
(`PooledReader0` / `channel` / `conn`).

Positional binding has a second benefit: a game update that renames parameters cannot affect us.
To rewrite a parameter, declare it `ref`. All three behaviours — `__N` binding, `ref __N` write-back,
and a prefix returning false to skip the original — were verified against the game's own
`0Harmony.dll` (HarmonyX 2.9.0).

### The readers need TargetRpcs filtered out

`Server` carries 57 readers: the 56 ServerRpc ones take
`(PooledReader, Channel, NetworkConnection)`, and one more,
`TargetReconcileRejectedItemPickup`, is a TargetRpc with **only two parameters**. Binding `__2` to it
throws at patch time, so parameter count and types are checked first.

### A guard must never throw

FishNet treats *any* exception thrown while parsing or executing an RPC as malformed data and
**kicks the sender outright** (`ServerManager.Kick(KickReason.MalformedData)`, in FishNet.Runtime's
`Managing/Server/ServerManager.cs:1111-1119`).

So the guards only do null checks, comparisons and connection matching; anything that needs
reflection or a scene scan is wrapped in its own try/catch. **A bug in the guard must not turn into
a kick.**

### The reader teardown is a finalizer, not a postfix

Step (3) above has to run even when the reader throws, and a Harmony **postfix does not run on an
exception** — while a reader is exactly the code that parses bytes off the network, so malformed
packets, destroyed references and other mods patching the same method can all make it throw.

Missing one `Sender.End()` does not just lose a single check: `Sender.Current` keeps **the previous
sender**, and the game itself sends RPCs on everyone's behalf from the server in several places
(explosion damage, expired explosives, boss attacks). Until the next reader arrives, those internal
calls get validated as though that player had sent them.

The window is narrow — `Sender.Begin` overwrites unconditionally, so the next RPC repairs it — but
that is convergence by luck rather than by design. A finalizer with a void return runs either way
and does not swallow the exception.

## What is checked

### Actor identity (`Check Actor Identity`)

The `Player` named in the RPC parameters (or the holder of the item) must be the connection that
sent the packet.

This single check stops most griefing: moving other players, teleporting them off the map, emptying
their inventories, switching their hotbar, buying attachments for their gun with the team's money,
speaking in their name, kicking them out of the driver's seat.

Verification compares `player.Owner` against the `conn` the reader supplied — `Player` is spawned
with `base.Spawn(player.gameObject, conn, ...)`, so its owner is whoever sent `SpawnPlayer`.

**The host exemption uses three independent signals, not just `IsLocalClient`.** This was learned
the hard way. `NetworkConnection.IsLocalClient` is
`NetworkManager != null && NetworkManager.ClientManager.Connection == this`, and
`NetworkConnection.NetworkManager` is a field that may never have been set — FishNet itself patches
around this on the `GetAddress()` path with
`if (NetworkManager == null) NetworkManager = InstanceFinder.NetworkManager;`. Once that field is
null, `IsLocalClient` **quietly returns false**, every packet from the host gets checked as a
stranger's, and it looks exactly like "playing normally trips the guards".

So the check is now: `IsLocalClient`, then compare `ClientId` directly against
`ClientManager.Connection` (without going through `conn.NetworkManager`), then compare against
`Player.LocalPlayer.Owner` (which does not touch FishNet's connection semantics at all). Any one of
them is enough. The first time a connection is judged "not the host", all three ids are printed to
the log, so a misjudgement is visible at a glance.

**"Who is entitled" is not always the holder.** Each RPC has to be matched against the gate at its
own send site; copying one rule everywhere blocks people who are playing normally:

| Gate | Applies to | Examples |
|---|---|---|
| Holder | things in your hands | `ReloadWeapon`, `SetItemSkin`, `UpdateHeldToolPosRot` |
| **Simulator** | things nobody is holding, but some client is running physics for | `GrillItemInLava` |
| Driver | boats | `SendBoatInput`, `SetDriver(null)` |
| The target's own state | cases with no "actor" to verify | `SetItemMultiplier` |

`GrillItemInLava` is the case that taught us this: its only send site,
`MainLava.ItemTouchedLava` (`MainLava.cs:96`), gates on **`item.RigidbodySync.IsSimulatedLocal`**,
not on the holder — nobody is holding the thing you threw into the lava. So the check compares
`RigidbodySync.SyncedSimulator` (a SyncVar, correct on the server) against the sender. There is no
race: `StartSimulateLocal` sends `SetSyncedSimulator` **before** `ToggleSimulation(true)`, and both
RPCs are Reliable and therefore ordered.

`SetItemMultiplier` has no actor to verify either, and its effect is **irreversible**:
`Item.SetKillscoreMultiplier` starts with `if (_killScoreMultiplier.Value != 1f) return;`
(`Item.cs:893`), so an item can only be set once. Racing to set `0` on the fish someone just caught
means that fish can never be sold for anything (`Item.TotalWorth` multiplies by it at the end).

Its send site is `Creature.LocalHit` (`Creature.cs:397`) — the killer's client sets the multiplier on
the creature that just died, and the killer is neither the holder nor the simulator (a fish shot in
the water is not held by anyone). So the check is "has the sender hit this creature recently?": that
information already passes through the `HitCreature` guard, so it is recorded in `Damagers` on the
way past. Ordering is guaranteed — `LocalHit` sends `HitCreature` before `SetItemMultiplier`, and
both are Reliable and ordered.

**It must not be "the last person who hit it"** (which is what the first version did). There is a
network round trip between the two RPCs, and when several people beat on the same boss or the same
school of fish, B's non-lethal hit landing between A's `HitCreature` and A's `SetItemMultiplier` is
**normal**. One record per creature gets overwritten, A's legitimate kill is judged "invalid target",
and that is a category that counts as violation evidence — accumulate it and you kick an innocent
guest. So `Damagers` records *everyone* who hit that creature within 10 seconds, and the sender
passes if they are in that set. An attacker still has to actually hit the creature to earn the right
to set a multiplier.

**Do not use `Creature.IsDead` as a condition** (the first version did; one session blocked 43
legitimate kills). It is `Hp <= 0`, and `Creature.Hp` is an **ordinary auto-property** written only in
the **client half** of `OnStartClient` and `OnHealthChange` — and that callback's first line is
`if (asServer) return;` (`Creature.cs:496`). The server changes the SyncVar `_hp.Value`; the `Hp`
mirror only catches up on the client callback, so at the moment the RPC arrives it still holds the
old value. It reads like server state and is in fact a client mirror.

### Prices (`Check Purchase Prices`)

Bait, boat motor and boat radar pass the price as a parameter for the client to fill in. **The fix is
not a hardcoded price table** — it is to ask the shop object in the scene: the price is
`Purchasable._customCost`, which is exactly what the game itself sends.

What is accepted is a *set* of values rather than a single one: the same bait can exist at a paid
stall and a free one at the same time (`_isFree` makes `_customCost` 0), and the tutorial area's
bait is free. When the price does not match, the RPC is **not blocked** — the correct price is
substituted and it goes through, with a note recorded.

Only enabled objects are scanned: `BaitPurchasable._customCost` is computed in `Awake`, so on an
object that has never woken the field is still 0, and collecting it would add a phantom "this bait
is free" entry.

`BuyItem`'s `isFree` boolean is the same story — passing `true` skips the deduction entirely, so the
guard rewrites it to `false`.

### Values (`Check Value Ranges`)

| Item | Rule |
|---|---|
| Damage to a creature | `0 ... Maximum Damage To A Creature`. **Negatives are always rejected** — `ServerChangeHp` is `_hp.Value -= damage`, so negative damage heals the boss |
| Damage to a player (with an attacker) | `0 ... Maximum Damage To A Player` |
| Damage to a player (no attacker) | `0 ... Maximum Sourceless Damage`, plus its own rate bucket |
| Score multiplier | `0 ... Maximum Score Multiplier` |
| Revive progress | Clamped to `0...1` |
| Projectiles per shot | At most `Maximum Projectiles Per Shot` |
| Chat length | Truncated past the limit |
| Position / rotation / frequency | NaN and infinity rejected |

Zero damage is allowed through: the server already does nothing with it (`if (A_2 != 0)`), so
blocking it would only manufacture false violations.

### Indices (`Check Index Ranges`)

These are not cheats, they are **crashes**: the game's own guards are missing a lower bound, and any
exception gets the sender kicked.

- `UnlockPocket`: `_extraSlotCosts[index - 1]`, where a `byte 0` is promoted to int in the
  subtraction and becomes **-1** (not 255). The game only checks `> 5`.
- `BuyBait`: `_ownedBaits[index - 1]`, the same trap.
- `TakeItemFromNpc`: the guard reads `if (A_2 != 255 && !NpcIsHoldingItem(A_2)) return;` — **an id of
  255 skips the whole guard**, and execution walks straight into the dictionary indexer
  `_idToNpc[255]`.
- `PlaceBet` / `UpdateRoulette` / `TakeItemFromNpc`: `CasinoManager.Instance` and `NPCManager.Instance`
  are plain static fields, assigned in `Awake` and never cleared on destroy, so after leaving that
  level they are "destroyed but not null" Unity objects — dereferencing them in the original method
  throws `MissingReferenceException`.

### Rates (`Rate Limiting`)

One token bucket per RPC per connection. A bucket rather than a fixed-window counter, because normal
play is bursty by nature (a magazine emptied, a row of items picked up) and a fixed window would cut
those off.

The limits live in the individual guards (position updates 200/s, item positions 800/s, purchases
10/s, chat 3/s, and so on), and `Rate Limit Multiplier` scales all of them at once.

**Rate drops do not count towards enforcement.** Dropping the packet has already stopped the flood;
meanwhile normal play emits small bursts from stutter, loading and network jitter, and treating
those as cheating evidence eventually kicks someone who did nothing wrong. So the panel keeps
"violations" and "drops" in separate columns, and the log separates them into Warning and Info.

Three things changed after live testing:

- **The clock cannot be `Time.unscaledTime`.** It only updates once per frame, while FishNet
  processes a whole queued batch of packets within a single frame (even more so after a stutter,
  when several ticks catch up). Every packet in that frame gets the same timestamp, the bucket never
  refills, and "the burst after a stutter" is guaranteed to hit the limit. Now it uses
  `Time.realtimeSinceStartup`.
- **The bucket has to be big enough**: `rate x 0.5` gave only half a second of headroom, now
  `rate x 2`.
- **`HandOverItemSimulation` is flooded by the game itself.**
  `ItemExtraRigidbody.OnCollisionStay` (`ItemExtraRigidbody.cs:133`) sends one per physics step per
  contact pair for any living boss creature — a boss leaning on terrain is several hundred a second.
  The original 30/s was a wild underestimate; one test session produced over two thousand entries.
  Its effect is nearly always redundant anyway (`StartSimulateLocal` returns early when it is already
  simulating locally), so the limit was relaxed to 400/s.

### Connection layer

- **One Player per connection**: there was no such check, so sending `SpawnPlayer` twice spawned a
  second one.
- **Ban list**: `Kick` only drops the connection and the other side can reconnect immediately. The
  list is matched when a connection is established, against the Steam ID from
  `NetworkConnection.GetAddress()` — a transport-layer source, not forgeable.

## What it cannot stop (stated plainly)

- **Player damage with no attacker.** `HitPlayer`'s friendly fire check is
  `if (A_6 && !UseFriendlyFire) return;` — pass `null` as the attacker and the whole check is
  skipped. And a `null` attacker is legitimate (drowning, creature collisions, bosses), while the
  game lets **any** client send those on a creature's behalf (`AttackingFish.DamageOnCollision` only
  looks at `_rigSync.IsSimulatedLocal`). There is no identity to verify; all that is left is a cap
  and a rate limit.
- **Bait that exists at a free stall** can be taken for free without limit, because `cost = 0` is a
  legitimate value for that bait.
- **No identity is available on a non-Steam transport.** When `conn.GetAddress()` cannot resolve a
  SteamID, the ban list, the impersonation rewrite and the panel's Steam ID column stop working (the
  other guards carry on). The first occurrence is logged — it does not pass silently.
- **Small-scale speed hacks and flight.** The movement speed check is off by default; see below.
- **Money is one number shared by the team** (`MoneyManager.Money`), not per-player. The identity
  check stops "buying on someone else's behalf"; it cannot stop "spending the team's money on
  yourself". That would need the game's economy model changed.

## Why the movement speed check is off by default

Position updates ride an unreliable channel, and packet loss, reordering, island teleports and
boarding a boat all make "distance between two updates divided by time" spike. So the logic is
conservative to the point of gentleness: more than a second since the last **accepted** update and it
only resets the baseline without judging; the speed has to stay over the limit for 0.35 seconds
before anything is dropped; and a drop only discards that position packet — nobody is kicked.

**When over the limit, the baseline position and time freeze together.** The first version got this
wrong and it is worth writing down: freezing only the position while the time advanced to `now` made
`dt` permanently one frame while the distance was the whole displacement since the baseline, so the
computed speed was always over the limit. A teleported player would be blocked **permanently**, with
standing still no help, because the "reset the baseline after a second with no update" escape hatch
was itself jammed by the advancing timestamp. With both frozen, `dt` grows with time and the computed
speed falls, so "it was only lag" recovers on its own, while a genuine teleport is cleaned up at
worst a second later by the baseline reset.

Even so it still misjudges, and it still cannot stop small speed increases — the cost/benefit was
never good. It is there for when you already know someone is flying.

## The panel (F11 by default)

Three tabs:

- **Connections** — per person: violations, rate drops, Steam ID, the reason they were last blocked,
  plus kick and ban buttons. **"Drops" is always greyed out**: it does not count towards enforcement,
  so a large number there does not mean anything is wrong.
- **Events** — the last 80 blocked actions (who, which RPC, why, how long ago).
- **Bans** — the list, with per-entry removal.

Kick and ban are **two-step**: the first press turns the button into "Sure?", the second sends it,
and three seconds of inaction cancels.

The whole panel is drawn with fixed-`Rect` `GUI.*` calls and **no GUILayout**. The other IMGUI mods in
this project follow a rule about deferring layout-changing state to the Layout event; that is a
GUILayout-specific problem — it builds a control tree on the Layout frame and later events index into
that tree, hitting a NullReferenceException when they disagree. Fixed rects have no such tree, so
switching tabs, appending an event row, or turning a button into "Sure?" can all take effect
immediately. The price is computing coordinates by hand.

The connection list snapshot is computed in `Update` and only read in `OnGUI`: `OnGUI` runs several
times per frame, and `GetAddress()` reaches into the transport layer — not something to call a dozen
times a frame.

## Settings

`BepInEx/config/htf.guardian.cfg`, written on first run. Everything is also editable in game through
[HtF Config Menu](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.ConfigMenu) (F9).

| Section | Settings |
|---|---|
| General | Enabled, Also Check The Host |
| Validation | Check Actor Identity / Purchase Prices / Value Ranges / Index Ranges, Rate Limiting, Rate Limit Multiplier, Only The Host May End The Run, Movement Speed Check, Maximum Movement Speed |
| Limits | Damage to a player, sourceless damage, damage to a creature, score multiplier, projectiles per shot, chat length |
| Enforcement | Violation Limit, Violation Decay, Action (Log Only / Kick / Kick And Ban), Report Cooldown, Announce In Chat |
| Interface | Panel Key, UI Scale, Font |

**Enforcement starts on "Log Only".** Watch the panel for a few sessions before letting it kick
people automatically — latency alone produces the occasional false positive, and kicking the wrong
person is harder to undo than missing one cheat.

**Violations decay over time** (by default, 60 seconds of clean play forgives one). Without decay,
"violation limit 40" really means "40 in a lifetime": the counter only ever goes up, so the headroom
is consumed monotonically and a perfectly normal player hits the limit after a few hours. With decay,
the limit means "a burst of violations" — which is the thing actually worth catching.

**"Also Check The Host" is off by default**: the host's connection *is* the server, and several of the
game's own systems send RPCs on everyone's behalf from there (`ExplosionManager.ServerExplode`
damage, expired explosives, boss attacks). Checking those with the normal rules is guaranteed to
misfire.

The ban list lives at `BepInEx/config/HtF.Guardian/banned.txt`, one Steam ID per line, `#` starts a
comment. If you edit the file while the game is running, press "Reload list" on the panel.

### Why the .cfg file is in Chinese

The section and key names inside the `.cfg` are Chinese, and they stay that way in every language.
They are identifiers, not labels: BepInEx uses them to find your saved values, so translating them
would make every setting you had tuned look like a brand new one and reset it to default. The names
in the table above are what the in-game settings page shows you.

## Using it with HtF Dazed Tools

[HtF Dazed Tools](https://github.com/HHim8826/HtF-Mods/tree/main/mods/HtF.DazedTools) sends exactly
these RPCs. **They do not conflict while you are the host** — the host is exempt by default, so the
commands still work. Use Dazed Tools in someone else's lobby while they run Guardian and those
commands get blocked, which is the intended behaviour.

## Compatibility

- *How to Fish* 1.0.9, Unity 6000.4.4f1 (Mono)
- BepInEx 5.4.23.5, Harmony 2.9

## Building

```bash
dotnet build mods/HtF.Guardian/HtF.Guardian.csproj
```

Paths can be overridden with `-p:GameManaged="..."` / `-p:ProfileDir="..."`; the defaults live in
`../Common.props`.

## Not yet verified in game

Verified at the code level: the parameter positions and types of all 56 RPCs, and the guards' return
types, were checked one by one with `MetadataLoadContext` against **the exact `Assembly-CSharp.dll`
the game loads** (63 positional bindings, 0 mismatches). `__N` positional injection and `ref`
write-back were verified by running against the game's own `0Harmony.dll`.

Still needing a real session: the false-positive rate in a populated lobby, the real headroom of each
rate limit, and what a blocked action looks like on the client (local prediction has already played
the effect and the server did not accept it, so you see position or health snap back).

## Links

- [Source, and the other six HtF mods](https://github.com/HHim8826/HtF-Mods)
- [Changelog](https://github.com/HHim8826/HtF-Mods/blob/main/mods/HtF.Guardian/CHANGELOG.md)
- MIT licensed

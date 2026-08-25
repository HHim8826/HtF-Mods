using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HtF.DazedTools.Commands
{

public static class CommandCore
{
	public static bool IsServerCommand(string fullCommand)
	{
		if (string.IsNullOrEmpty(fullCommand))
		{
			return false;
		}
		if (!fullCommand.StartsWith("/"))
		{
			return false;
		}
		if (!Server.Instance)
		{
			ChatManager.ChatMessage("Server instance not found");
			return true;
		}
		string[] array = fullCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return false;
		}
		string text = array[0];
		string text2 = "";
		for (int i = 1; i < array.Length; i++)
		{
			text2 += array[i];
		}
		text = text.Remove(0, 1).ToLowerInvariant();
		// Only the tokens AFTER the command name are arguments. The old code left the
		// command name itself in here whenever no arguments were supplied, which made
		// every "args.Length == 0" guard below unreachable - so e.g. a bare /hitplayer
		// resolved "/hitplayer" to the local player and killed you instead of printing
		// the usage line.
		string[] array2 = (array.Length > 1) ? array.Skip<string>(1).ToArray<string>() : Array.Empty<string>();
		if (text == "spawn" && !string.IsNullOrEmpty(text2))
		{
			CommandCore.UseSpawnCommand(text2, false);
		}
		else if (text == "spawndead" && !string.IsNullOrEmpty(text2))
		{
			CommandCore.UseSpawnCommand(text2, true);
		}
		else if (text == "spawndrip" && !string.IsNullOrEmpty(text2))
		{
			CommandCore.UseSpawnDripCommand(text2, false);
		}
		else if (text == "spawndripdead" && !string.IsNullOrEmpty(text2))
		{
			CommandCore.UseSpawnDripCommand(text2, true);
		}
		else if (text == "grill")
		{
			CommandCore.UseGrillCommand();
		}
		else if (text == "boat")
		{
			CommandCore.UseBoatCommand();
		}
		else if (text == "addmoney")
		{
			CommandCore.UseAddMoneyCommand(array2);
		}
		else if (text == "removemoney")
		{
			CommandCore.UseRemoveMoneyCommand(array2);
		}
		else if (text == "nextisland")
		{
			CommandCore.UseNextIslandCommand(false);
		}
		else if (text == "previsland")
		{
			CommandCore.UseNextIslandCommand(true);
		}
		else if (text == "godmode")
		{
			CommandCore.UseGodModeCommand();
		}
		else if (text == "oneshot")
		{
			CommandCore.UseOneShotCommand();
		}
		else if (text == "killallcreatures")
		{
			CommandCore.UseToggleAllCreaturesKilledCommand(true, false);
		}
		else if (text == "killalldripcreatures")
		{
			CommandCore.UseToggleAllCreaturesKilledCommand(true, true);
		}
		else if (text == "resetallcreatures")
		{
			CommandCore.UseToggleAllCreaturesKilledCommand(false, false);
		}
		else if (text == "resetalldripcreatures")
		{
			CommandCore.UseToggleAllCreaturesKilledCommand(false, true);
		}
		else if (text == "slots")
		{
			CommandCore.UseSlotsCommand(array2);
		}
		else if (text == "killboss")
		{
			CommandCore.UseKillBossCommand();
		}
		else if (text == "allskins")
		{
			CommandCore.UseUnlockAllSkinsCommand();
		}
		else if (text == "noskins")
		{
			CommandCore.UseLockAllSkinsCommand();
		}
		else if (text == "showkillscores")
		{
			CommandCore.UseShowKillScoresCommand();
		}
		else if (text == "finishgame")
		{
			CommandCore.UseFinishGameCommand(array2);
		}
		else if (text == "unlockachievements")
		{
			CommandCore.ToggleAchievements(true);
		}
		else if (text == "lockachievements")
		{
			CommandCore.ToggleAchievements(false);
		}
		// --- 🔴 CRITICAL ServerRPC Vulnerabilities ---
		else if (text == "buyfree" || text == "buyitemfree" || text == "freebuy")
		{
			CommandCore.UseBuyFreeCommand(array2);
		}
		else if (text == "hitcreature" || text == "healcreatures")
		{
			CommandCore.UseHitCreatureCommand(array2);
		}
		else if (text == "hitplayer" || text == "killplayer")
		{
			CommandCore.UseHitPlayerCommand(array2);
		}
		// --- 🟠 HIGH ServerRPC Vulnerabilities ---
		else if (text == "buybaitfree" || text == "freebait")
		{
			CommandCore.UseBuyBaitFreeCommand(array2);
		}
		else if (text == "buymotorfree" || text == "freemotor")
		{
			CommandCore.UseBuyMotorFreeCommand(array2);
		}
		else if (text == "buyradarfree" || text == "freeradar")
		{
			CommandCore.UseBuyRadarFreeCommand(array2);
		}
		else if (text == "forcerespawn")
		{
			CommandCore.UseRespawnPlayerCommand(array2);
		}
		else if (text == "tpplayer")
		{
			CommandCore.UseTeleportPlayerCommand(array2);
		}
		else if (text == "forceplayerpos")
		{
			CommandCore.UseForcePlayerPosCommand(array2);
		}
		else if (text == "forcedropall" || text == "dropallitems")
		{
			CommandCore.UseDropAllItemsCommand(array2);
		}
		else if (text == "detonateexplosives" || text == "detonateall" || text == "detonateexplosive")
		{
			CommandCore.UseActivateExplosiveCommand(array2);
		}
		else if (text == "takenpcitem")
		{
			CommandCore.UseTakeItemFromNpcCommand(array2);
		}
		else if (text == "setitemmultiplier" || text == "setmultiplier")
		{
			CommandCore.UseSetItemMultiplierCommand(array2);
		}
		else if (text == "sendfinishgame" || text == "forcefinishgame")
		{
			CommandCore.UseSendFinishGameCommand(array2);
		}
		// --- 🟡 MEDIUM ServerRPC Vulnerabilities ---
		else if (text == "removeplayeritem")
		{
			CommandCore.UseRemoveItemFromInventoryCommand(array2);
		}
		else if (text == "setitemholder" || text == "stealhelditem")
		{
			CommandCore.UseSetItemHolderCommand(array2);
		}
		else if (text == "hijackitemphysics")
		{
			CommandCore.UseSetSyncedSimulatorCommand(array2);
		}
		else if (text == "tpitems" || text == "gatheritems")
		{
			CommandCore.UseUpdateItemPosRotCommand(array2);
		}
		else if (text == "forceresurrect")
		{
			CommandCore.UseResurrectPlayerCommand(array2);
		}
		else if (text == "forcefinisheating")
		{
			CommandCore.UseFinishEatingCreatureCommand(array2);
		}
		else if (text == "setboatdriver")
		{
			CommandCore.UseSetDriverCommand(array2);
		}
		else if (text == "steerboat" || text == "sendboatinput")
		{
			CommandCore.UseSendBoatInputCommand(array2);
		}
		else if (text == "forceplacebet")
		{
			CommandCore.UsePlaceBetCommand(array2);
		}
		else if (text == "spoofroulette")
		{
			CommandCore.UseUpdateRouletteCommand(array2);
		}
		else if (text == "spoofprojectile")
		{
			CommandCore.UseSpoofProjectileCommand(array2);
		}
		else if (text == "grillhelditem" || text == "grillitemlava" || text == "lavacook")
		{
			CommandCore.UseGrillItemInLavaCommand();
		}
		else if (text == "forcesetafk")
		{
			CommandCore.UseSetIsAfkCommand(array2);
		}
		else if (text == "forceunlockpocket")
		{
			CommandCore.UseUnlockPocketCommand(array2);
		}
		else if (text == "forcebuyattachment")
		{
			CommandCore.UseBuyAttachmentCommand(array2);
		}
		else if (text == "forcebuybulletupgrade")
		{
			CommandCore.UseBuyBulletUpgradeCommand();
		}
		else if (text == "forcebuysharpnessupgrade")
		{
			CommandCore.UseBuySharpnessUpgradeCommand();
		}
		else if (text == "spoofprojectilehit")
		{
			CommandCore.UseProjectileHitDynamicCommand(array2);
		}
		else if (text == "vulnhelp" || text == "cmdhelp")
		{
			CommandCore.UseVulnHelpCommand();
		}
		else
		{
			ChatManager.ChatMessage("Couldn't find command called <b>" + fullCommand + "</b> (Type /vulnhelp for list)");
		}
		return true;
	}

	private static void ToggleAchievements(bool unlock)
	{
		AchievementManager.ToggleAllAchievements(unlock);
	}

	private static void UseFinishGameCommand(string[] args)
	{
		if (!EndGameManager.Instance)
		{
			ChatManager.ChatMessage("EndGameManager instance not found");
			return;
		}
		// This ends the run for everybody and rolls the credits - that is what "the
		// command threw me back to the main menu" actually was. Require confirmation.
		if (!CommandCore.HasConfirmArg(args))
		{
			ChatManager.ChatMessage("<b>/finishgame</b> ends the run for EVERYONE. Type <b>/finishgame confirm</b> if you really mean it.");
			return;
		}
		EndGameManager.Instance.FinishGameInput();
	}

	private static void UseShowKillScoresCommand()
	{
		for (int i = 0; i < 3; i++)
		{
			List<Bonus> allBonuses = KillScoreCalculator.GetAllBonuses(i);
			Player.LocalPlayer.KillScore.AddKillScore(string.Format("All killscores {0}", i), 100, allBonuses);
		}
	}

	private static void UseUnlockAllSkinsCommand()
	{
		SaveManager.LockAllSkins();
		foreach (Item item in GameInfo.ItemWithSkinsforCommands)
		{
			if (item.SkinPreset)
			{
				for (int j = 0; j < item.SkinPreset.Skins.Count; j++)
				{
					SaveManager.UnlockSkin(item.ID, (byte)j);
				}
			}
		}
		for (int k = 0; k < BoatManager.Boat.SkinPreset.Skins.Count; k++)
		{
			SaveManager.UnlockSkin(byte.MaxValue, (byte)k);
		}
	}

	private static void UseLockAllSkinsCommand()
	{
		SaveManager.LockAllSkins();
	}

	private static void UseKillBossCommand()
	{
		if (!BossManager.Boss)
		{
			return;
		}
		BossManager.Boss.LocalHit(BossManager.Boss.transform, BossManager.Boss.transform.position, Vector3.up, Player.LocalPlayer, 999999, false, Vector3.zero, false);
	}

	private static void UseSlotsCommand(string[] seperatedSubCommands)
	{
		if (seperatedSubCommands.Length < 2)
		{
			return;
		}
		Item spawnable = GameInfo.GetSpawnable(seperatedSubCommands[0]);
		SkinPreset skinPreset = (spawnable ? spawnable.SkinPreset : (BoatManager.Boat ? BoatManager.Boat.SkinPreset : null));
		if (!skinPreset || skinPreset.Skins == null)
		{
			ChatManager.ChatMessage("No skin preset found for <b>" + seperatedSubCommands[0] + "</b>");
			return;
		}
		byte b = (byte)skinPreset.Skins.Count;
		byte b2;
		if (byte.TryParse(seperatedSubCommands[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out b2) && b2 < b)
		{
			SlotMachineManager.SetCheatSkin(spawnable, b2);
		}
		else
		{
			ChatManager.ChatMessage("Skin index must be between 0 and " + (b - 1));
		}
	}

	private static void UseToggleAllCreaturesKilledCommand(bool to, bool drip)
	{
		GameInfo.ToggleAllCreaturesKilled(to, drip);
	}

	private static void UseSpawnCommand(string subCommands, bool asDead = false)
	{
		Item spawnable = GameInfo.GetSpawnable(subCommands.Replace(" ", "").ToLowerInvariant());
		if (!spawnable)
		{
			ChatManager.ChatMessage("Couldn't find spawnable called <b>" + subCommands + "</b>");
			return;
		}
		// Item.Creature is the serialized _creature field, which only creature prefabs assign,
		// and the picker offers every spawnable (Radio, guns, tools). Without this the dead
		// variant threw a NullReferenceException, same bug UseSpawnDripCommand already guards.
		// Checked before Instantiate on purpose: bailing out afterwards would leave an
		// un-spawned clone sitting in the scene.
		if (asDead && !spawnable.Creature)
		{
			ChatManager.ChatMessage("<b>" + subCommands + "</b> is not a creature");
			return;
		}
		Vector3 vector = GameInfo.CurCamera.transform.position + GameInfo.CurCamera.transform.forward * 2f;
		Item item = UnityEngine.Object.Instantiate<Item>(spawnable, vector, Quaternion.identity);
		if (asDead)
		{
			item.Creature.ServerKillOnSpawn();
		}
		Server.Instance.Spawn(item.gameObject, null, default(Scene));
	}

	private static void UseSpawnDripCommand(string subCommands, bool asDead = false)
	{
		// GameInfo.GetSpawnable returns a real null for unknown names, so calling
		// GetComponent on the result directly threw a NullReferenceException.
		Item spawnable = GameInfo.GetSpawnable(subCommands.Replace(" ", "").ToLowerInvariant());
		if (!spawnable)
		{
			ChatManager.ChatMessage("Couldn't find spawnable called <b>" + subCommands + "</b>");
			return;
		}
		Creature component = spawnable.GetComponent<Creature>();
		if (!component)
		{
			ChatManager.ChatMessage("<b>" + subCommands + "</b> is not a creature");
			return;
		}
		Vector3 vector = GameInfo.CurCamera.transform.position + GameInfo.CurCamera.transform.forward * 2f;
		Creature creature = UnityEngine.Object.Instantiate<Creature>(component, vector, Quaternion.identity);
		creature.SetDrip();
		if (asDead)
		{
			creature.Creature.ServerKillOnSpawn();
		}
		Server.Instance.Spawn(creature.gameObject, null, default(Scene));
	}

	private static void UseGrillCommand()
	{
		NPCManager.UnlockGrill();
	}

	private static void UseBoatCommand()
	{
		BoatManager.Boat.UnlockBoat();
	}

	private static void UseAddMoneyCommand(string[] args)
	{
		int amount = Mathf.Abs(CommandCore.ParseInt(args, 0, 9999));
		MoneyManager.AddMoney(amount, Player.LocalPlayer);
		ChatManager.ChatMessage("Added " + amount + " (now " + MoneyManager.Money + ")");
	}

	private static void UseRemoveMoneyCommand(string[] args)
	{
		int amount = Mathf.Abs(CommandCore.ParseInt(args, 0, 9999));
		MoneyManager.RemoveMoney(amount, Player.LocalPlayer);
		ChatManager.ChatMessage("Removed " + amount + " (now " + MoneyManager.Money + ")");
	}

	private static void UseNextIslandCommand(bool prev = false)
	{
		OnlineIslandManager.TpToNextIsland(prev);
	}

	private static void UseGodModeCommand()
	{
		PlayerManager.ToggleGodMode();
	}

	private static void UseOneShotCommand()
	{
		ServerSettings.Instance.ToggleOneShot();
	}

	// ==========================================
	// Helper Methods for Player & Target Resolution
	// ==========================================

	// How many entities a single batch command ("all", /tpitems, /detonateall, ...) may
	// touch. Every one of those used to fire one reliable ServerRpc per item in a single
	// frame, and the server then fanned each one back out as an ObserversRpc to every
	// client - easily hundreds of packets per tick, which overflows the transport queue
	// and drops you back to the main menu.
	private const int MaxBatchTargets = 48;

	// Upper bound for /spoofprojectile: the velocity array is serialised into one packet,
	// and the count came straight from user input (new Vector3[2000000000] -> OOM).
	private const int MaxSpoofedProjectiles = 64;

	// /hitcreature and /forcefinisheating used to pick the nearest creature anywhere on
	// the map. Keep it to something you can plausibly be aiming at.
	private const float MaxTargetRange = 60f;

	// ---- Culture-invariant parsers that KEEP the caller's default on failure. ----
	// int.TryParse & friends write default(T) into the out parameter when parsing fails,
	// so the old "int damage = 999999; int.TryParse(args[0], out damage);" pattern
	// silently reset every default to 0. Parsing was also culture-sensitive, so "1.5"
	// failed outright under a comma-decimal locale.

	private static int ParseInt(string[] args, int index, int fallback)
	{
		int result;
		if (args != null && index >= 0 && index < args.Length && int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return fallback;
	}

	private static uint ParseUInt(string[] args, int index, uint fallback)
	{
		uint result;
		if (args != null && index >= 0 && index < args.Length && uint.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return fallback;
	}

	private static byte ParseByte(string[] args, int index, byte fallback)
	{
		byte result;
		if (args != null && index >= 0 && index < args.Length && byte.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return fallback;
	}

	private static float ParseFloat(string[] args, int index, float fallback)
	{
		float result;
		if (args != null && index >= 0 && index < args.Length && float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return fallback;
	}

	private static bool ParseBool(string[] args, int index, bool fallback)
	{
		bool result;
		if (args != null && index >= 0 && index < args.Length && bool.TryParse(args[index], out result))
		{
			return result;
		}
		return fallback;
	}

	private static bool TryParseVector3(string[] args, int index, out Vector3 pos)
	{
		pos = Vector3.zero;
		if (args == null || index < 0 || index + 2 >= args.Length)
		{
			return false;
		}
		float x;
		float y;
		float z;
		if (!float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(args[index + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
		{
			return false;
		}
		pos = new Vector3(x, y, z);
		return true;
	}

	private static bool HasConfirmArg(string[] args)
	{
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i].Equals("confirm", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	// Returns null when the argument does not name a reachable player. The old version
	// fell back to Player.LocalPlayer, so a typo or an offline player silently retargeted
	// the command at yourself - "/hitplayer bob 999999" killed you, not bob.
	private static Player ResolvePlayer(string arg, out bool isAll)
	{
		isAll = false;
		if (string.IsNullOrEmpty(arg) || arg.Equals("me", StringComparison.OrdinalIgnoreCase) || arg.Equals("self", StringComparison.OrdinalIgnoreCase))
		{
			return Player.LocalPlayer;
		}
		if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			isAll = true;
			return null;
		}
		int index;
		bool isNumeric = int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
		if (isNumeric && index >= 0 && index < PlayerManager.Players.Count)
		{
			return PlayerManager.Players[index];
		}
		ulong steamId;
		if (ulong.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
		{
			foreach (Player player in PlayerManager.Players)
			{
				if (player && player.SteamID == steamId)
				{
					return player;
				}
			}
			// A purely numeric argument is an index or a SteamID and nothing else; do not
			// fall through to substring matching on names.
			return null;
		}
		if (isNumeric)
		{
			return null;
		}
		foreach (Player player2 in PlayerManager.Players)
		{
			if (player2 && !string.IsNullOrEmpty(player2.SteamName) && player2.SteamName.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return player2;
			}
		}
		return null;
	}

	private static Player ResolvePlayerOrReport(string arg, out bool isAll)
	{
		Player player = CommandCore.ResolvePlayer(arg, out isAll);
		if (!player && !isAll)
		{
			ChatManager.ChatMessage("Couldn't find player <b>" + arg + "</b>");
		}
		return player;
	}

	// For commands that only make sense against exactly one player. Returns null and
	// explains why, instead of returning silently on "all" or on an unknown name.
	private static Player ResolveSingleTarget(string arg, string commandName)
	{
		bool isAll;
		Player player = CommandCore.ResolvePlayer(arg, out isAll);
		if (isAll)
		{
			ChatManager.ChatMessage(commandName + " does not support 'all'");
			return null;
		}
		if (!player)
		{
			ChatManager.ChatMessage("Couldn't find player <b>" + arg + "</b>");
		}
		return player;
	}

	// Snapshot of the player list so a command that (indirectly) mutates PlayerManager
	// cannot invalidate the enumerator, and so the batch cap is applied consistently.
	private static List<Player> GetBatchPlayers(bool includeLocal)
	{
		List<Player> result = new List<Player>();
		foreach (Player player in PlayerManager.Players)
		{
			if (player && (includeLocal || player != Player.LocalPlayer))
			{
				result.Add(player);
				if (result.Count >= CommandCore.MaxBatchTargets)
				{
					break;
				}
			}
		}
		return result;
	}

	private static List<Item> GetBatchItems(Func<Item, bool> predicate)
	{
		List<Item> result = new List<Item>();
		foreach (Item item in ItemManager.Items.Values)
		{
			if (item && (predicate == null || predicate(item)))
			{
				result.Add(item);
				if (result.Count >= CommandCore.MaxBatchTargets)
				{
					ChatManager.ChatMessage("Capped at " + CommandCore.MaxBatchTargets + " items to avoid flooding the connection");
					break;
				}
			}
		}
		return result;
	}

	private static Item GetHeldItem()
	{
		return (Player.LocalPlayer && Player.LocalPlayer.Holding) ? Player.LocalPlayer.Holding.HeldItem : null;
	}

	// Deterministic replacement for "take whatever Dictionary.Values yields first", which
	// acted on an arbitrary item anywhere on the map - frequently somebody else's.
	private static Item GetNearestItem(Func<Item, bool> predicate)
	{
		if (!Player.LocalPlayer || !Player.LocalPlayer.Transform)
		{
			return null;
		}
		Vector3 pos = Player.LocalPlayer.Transform.position;
		Item nearest = null;
		float minSqrDist = float.MaxValue;
		foreach (Item item in ItemManager.Items.Values)
		{
			if (item && (predicate == null || predicate(item)))
			{
				float sqrMag = (item.transform.position - pos).sqrMagnitude;
				if (sqrMag < minSqrDist)
				{
					minSqrDist = sqrMag;
					nearest = item;
				}
			}
		}
		return nearest;
	}

	private static Creature GetTargetCreature()
	{
		if (!Player.LocalPlayer || !Player.LocalPlayer.Transform)
		{
			return null;
		}
		Vector3 pos = Player.LocalPlayer.Transform.position;
		Creature nearest = null;
		float maxSqrDist = CommandCore.MaxTargetRange * CommandCore.MaxTargetRange;
		float minSqrDist = maxSqrDist;
		foreach (Item item in ItemManager.Items.Values)
		{
			if (item && item.Creature)
			{
				float sqrMag = (item.transform.position - pos).sqrMagnitude;
				if (sqrMag < minSqrDist)
				{
					minSqrDist = sqrMag;
					nearest = item.Creature;
				}
			}
		}
		if (!nearest && BossManager.Boss && (BossManager.Boss.transform.position - pos).sqrMagnitude < maxSqrDist)
		{
			nearest = BossManager.Boss.GetComponent<Creature>();
		}
		return nearest;
	}

	// ==========================================
	// CRITICAL ServerRPC Vulnerability Commands
	// ==========================================

	// RPC #2 - BuyItem (CRITICAL: isFree = true bypasses cost)
	private static void UseBuyFreeCommand(string[] args)
	{
		if (args.Length == 0)
		{
			ChatManager.ChatMessage("Usage: /buyfree <itemId|itemName>");
			return;
		}
		Item spawnable;
		byte parsedId;
		if (byte.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedId))
		{
			spawnable = GameInfo.GetSpawnable(parsedId);
		}
		else
		{
			// Join every token so multi-word names work the same way /spawn accepts them
			// ("flying fish" -> "flyingfish"), matching how GameInfo keys the lookup.
			spawnable = GameInfo.GetSpawnable(string.Join("", args).Replace(" ", "").ToLowerInvariant());
		}
		// The old version sent the RPC with itemId 0 whenever the lookup failed.
		if (!spawnable)
		{
			ChatManager.ChatMessage("Couldn't find item <b>" + args[0] + "</b>");
			return;
		}
		if (!Player.LocalPlayer || !Player.LocalPlayer.Transform)
		{
			ChatManager.ChatMessage("Local player not ready");
			return;
		}
		byte itemId = spawnable.ID;
		Vector3 pos = Player.LocalPlayer.Transform.position + Player.LocalPlayer.Transform.forward * 2f;
		Server.Instance.BuyItem(itemId, Player.LocalPlayer, null, pos, Quaternion.identity, true);
		ChatManager.ChatMessage("Sent BuyItem RPC (isFree=true) for Item ID: " + itemId);
	}

	// RPC #3 - HitCreature (CRITICAL: Unbounded damage, negative heals)
	private static void UseHitCreatureCommand(string[] args)
	{
		int damage = CommandCore.ParseInt(args, 0, 999999);
		bool hitAll = args.Length > 1 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase);
		if (!Player.LocalPlayer)
		{
			ChatManager.ChatMessage("Local player not ready");
			return;
		}

		if (hitAll)
		{
			List<Item> creatures = CommandCore.GetBatchItems((Item it) => it.Creature);
			int count = 0;
			for (int i = 0; i < creatures.Count; i++)
			{
				Item item = creatures[i];
				if (item && item.Creature)
				{
					Server.Instance.HitCreature(item.Creature, Player.LocalPlayer, damage, item.transform.position, Vector3.up);
					count++;
				}
			}
			if (BossManager.Boss && count < CommandCore.MaxBatchTargets)
			{
				Creature bossCreature = BossManager.Boss.GetComponent<Creature>();
				if (bossCreature)
				{
					Server.Instance.HitCreature(bossCreature, Player.LocalPlayer, damage, BossManager.Boss.transform.position, Vector3.up);
					count++;
				}
			}
			ChatManager.ChatMessage("Sent HitCreature RPC to " + count + " creature(s) with damage: " + damage);
		}
		else
		{
			Creature target = CommandCore.GetTargetCreature();
			if (target)
			{
				Server.Instance.HitCreature(target, Player.LocalPlayer, damage, target.transform.position, Vector3.up);
				ChatManager.ChatMessage("Sent HitCreature RPC to target creature with damage: " + damage);
			}
			else
			{
				ChatManager.ChatMessage("No creature within " + CommandCore.MaxTargetRange + "m (add 'all' to hit everything)");
			}
		}
	}

	// RPC #4 - HitPlayer (CRITICAL: Attacker=null bypasses friendly fire, arbitrary victim/damage)
	private static void UseHitPlayerCommand(string[] args)
	{
		if (args.Length == 0)
		{
			ChatManager.ChatMessage("Usage: /hitplayer <playerIndex|name|me|all> [damage=999999] [bypassPvp=true]");
			return;
		}
		bool isAll;
		Player target = CommandCore.ResolvePlayerOrReport(args[0], out isAll);
		if (!target && !isAll)
		{
			return;
		}
		int damage = CommandCore.ParseInt(args, 1, 999999);
		bool bypassPvp = CommandCore.ParseBool(args, 2, true);
		Player attacker = (bypassPvp ? null : Player.LocalPlayer);

		if (isAll)
		{
			List<Player> targets = CommandCore.GetBatchPlayers(false);
			for (int i = 0; i < targets.Count; i++)
			{
				Player p = targets[i];
				if (p && p.Transform)
				{
					Server.Instance.HitPlayer(p, damage, Vector3.up * 5f, p.Transform.position, 1, attacker);
				}
			}
			ChatManager.ChatMessage("Sent HitPlayer RPC to " + targets.Count + " player(s) with damage: " + damage);
		}
		else if (target.Transform)
		{
			Server.Instance.HitPlayer(target, damage, Vector3.up * 5f, target.Transform.position, 1, attacker);
			ChatManager.ChatMessage("Sent HitPlayer RPC to " + target.SteamName + " with damage: " + damage);
		}
	}

	// ==========================================
	// HIGH ServerRPC Vulnerability Commands
	// ==========================================

	// RPC #5 - BuyBait (HIGH: Client-controlled cost -> free bait)
	private static void UseBuyBaitFreeCommand(string[] args)
	{
		byte baitIndex = CommandCore.ParseByte(args, 0, 1);
		int cost = CommandCore.ParseInt(args, 1, 0);
		// PlayerInventory.ServerBoughtBait does _ownedBaits[index - 1] in int arithmetic, so
		// index 0 reads [-1] and throws on the server -> FishNet kicks you. Index 0 is the
		// "no bait" entry anyway, so it was never buyable.
		int baitCount = (GameInfo.AllBaits != null) ? GameInfo.AllBaits.Count : 0;
		if (baitIndex < 1 || baitIndex >= baitCount)
		{
			ChatManager.ChatMessage("Bait index must be 1-" + (baitCount - 1) + " (0 would get you kicked)");
			return;
		}
		Server.Instance.BuyBait(Player.LocalPlayer, baitIndex, cost);
		ChatManager.ChatMessage("Sent BuyBait RPC for bait " + baitIndex + " with cost: " + cost);
	}

	// RPC #6 - BuyBoatMotor (HIGH: Client-controlled cost -> free motor upgrade)
	private static void UseBuyMotorFreeCommand(string[] args)
	{
		byte motorIndex = CommandCore.ParseByte(args, 0, 1);
		int cost = CommandCore.ParseInt(args, 1, 0);
		Server.Instance.BuyBoatMotor(Player.LocalPlayer, motorIndex, cost);
		ChatManager.ChatMessage("Sent BuyBoatMotor RPC for motor " + motorIndex + " with cost: " + cost);
	}

	// RPC #7 - BuyBoatRadar (HIGH: Client-controlled cost -> free radar)
	private static void UseBuyRadarFreeCommand(string[] args)
	{
		int cost = CommandCore.ParseInt(args, 0, 0);
		Server.Instance.BuyBoatRadar(Player.LocalPlayer, cost);
		ChatManager.ChatMessage("Sent BuyBoatRadar RPC with cost: " + cost);
	}

	// RPC #8 - RespawnPlayer (HIGH: No conn validation, targeting dead player)
	private static void UseRespawnPlayerCommand(string[] args)
	{
		bool isAll = false;
		Player target = Player.LocalPlayer;
		if (args.Length > 0)
		{
			target = CommandCore.ResolvePlayerOrReport(args[0], out isAll);
			if (!target && !isAll)
			{
				return;
			}
		}

		if (isAll)
		{
			List<Player> targets = CommandCore.GetBatchPlayers(true);
			for (int i = 0; i < targets.Count; i++)
			{
				Player p = targets[i];
				if (p && p.Transform)
				{
					Server.Instance.RespawnPlayer(p, p.Transform.position, p.Transform.rotation);
				}
			}
			ChatManager.ChatMessage("Sent RespawnPlayer RPC for " + targets.Count + " player(s)");
		}
		else if (target && target.Transform)
		{
			Server.Instance.RespawnPlayer(target, target.Transform.position, target.Transform.rotation);
			ChatManager.ChatMessage("Sent RespawnPlayer RPC for " + target.SteamName);
		}
	}

	// RPC #9 - TeleportPlayer (HIGH: Teleport arbitrary player anywhere)
	private static void UseTeleportPlayerCommand(string[] args)
	{
		if (args.Length == 0)
		{
			ChatManager.ChatMessage("Usage: /tpplayer <playerIndex|name|me|all> [x y z | void]");
			return;
		}
		bool isAll;
		Player target = CommandCore.ResolvePlayerOrReport(args[0], out isAll);
		if (!target && !isAll)
		{
			return;
		}
		Vector3 pos = (Player.LocalPlayer && Player.LocalPlayer.Transform) ? Player.LocalPlayer.Transform.position : Vector3.zero;
		if (args.Length >= 4)
		{
			if (!CommandCore.TryParseVector3(args, 1, out pos))
			{
				ChatManager.ChatMessage("Couldn't parse coordinates (use '.' as the decimal separator)");
				return;
			}
		}
		else if (args.Length == 2 && args[1].Equals("void", StringComparison.OrdinalIgnoreCase))
		{
			pos = new Vector3(0f, -500f, 0f);
		}

		if (isAll)
		{
			List<Player> targets = CommandCore.GetBatchPlayers(true);
			for (int i = 0; i < targets.Count; i++)
			{
				Player p = targets[i];
				if (p && p.Transform)
				{
					Server.Instance.TeleportPlayer(p, pos, p.Transform.eulerAngles.y);
				}
			}
			ChatManager.ChatMessage("Sent TeleportPlayer RPC for " + targets.Count + " player(s) to " + pos);
		}
		else if (target && target.Transform)
		{
			Server.Instance.TeleportPlayer(target, pos, target.Transform.eulerAngles.y);
			ChatManager.ChatMessage("Sent TeleportPlayer RPC for " + target.SteamName + " to " + pos);
		}
	}

	// RPC #10 - UpdatePlayerPosRot (HIGH: Force position/rotation of other players)
	private static void UseForcePlayerPosCommand(string[] args)
	{
		if (args.Length < 4)
		{
			ChatManager.ChatMessage("Usage: /forceplayerpos <playerIndex|name> <x> <y> <z>");
			return;
		}
		Player target = CommandCore.ResolveSingleTarget(args[0], "/forceplayerpos");
		if (!target)
		{
			return;
		}
		Vector3 pos;
		if (!CommandCore.TryParseVector3(args, 1, out pos))
		{
			ChatManager.ChatMessage("Couldn't parse coordinates (use '.' as the decimal separator)");
			return;
		}
		Server.Instance.UpdatePlayerPosRot(target, pos, Vector2.zero, false, true, Channel.Reliable);
		ChatManager.ChatMessage("Sent UpdatePlayerPosRot RPC for " + target.SteamName + " to " + pos);
	}

	// RPC #11 - DropAllItems (HIGH: Force arbitrary player to drop all items)
	private static void UseDropAllItemsCommand(string[] args)
	{
		bool isAll = false;
		Player target = Player.LocalPlayer;
		if (args.Length > 0)
		{
			target = CommandCore.ResolvePlayerOrReport(args[0], out isAll);
			if (!target && !isAll)
			{
				return;
			}
		}

		if (isAll)
		{
			List<Player> targets = CommandCore.GetBatchPlayers(true);
			for (int i = 0; i < targets.Count; i++)
			{
				Player p = targets[i];
				if (p && p.Transform)
				{
					Server.Instance.DropAllItems(p, p.Transform.position, Quaternion.identity);
				}
			}
			ChatManager.ChatMessage("Sent DropAllItems RPC for " + targets.Count + " player(s)");
		}
		else if (target && target.Transform)
		{
			Server.Instance.DropAllItems(target, target.Transform.position, Quaternion.identity);
			ChatManager.ChatMessage("Sent DropAllItems RPC for " + target.SteamName);
		}
	}

	// RPC #12 - ActivateExplosive (HIGH: Remote instant detonation of explosives)
	private static void UseActivateExplosiveCommand(string[] args)
	{
		List<Item> explosives = CommandCore.GetBatchItems((Item it) => it.Explosive);
		if (explosives.Count == 0)
		{
			ChatManager.ChatMessage("No explosives found in scene");
			return;
		}
		uint tick = InstanceFinder.TimeManager.Tick;
		int count = 0;
		for (int i = 0; i < explosives.Count; i++)
		{
			Item item = explosives[i];
			if (item && item.Explosive)
			{
				Server.Instance.ActivateExplosive(item.Explosive, tick, true, true, Player.LocalPlayer);
				count++;
			}
		}
		ChatManager.ChatMessage("Sent ActivateExplosive RPC for " + count + " explosive(s)");
	}

	// RPC #13 - TakeItemFromNpc (HIGH: Trigger NPC quest/unlock progress)
	private static void UseTakeItemFromNpcCommand(string[] args)
	{
		byte npcId = CommandCore.ParseByte(args, 0, 0);
		if (!NPCManager.Instance)
		{
			ChatManager.ChatMessage("No NPCManager on this island");
			return;
		}
		// The server skips its own NpcIsHoldingItem guard when the id is 255 and then
		// indexes a dictionary with it, throwing KeyNotFoundException. FishNet kicks the
		// sender on any exception raised while parsing an RPC, so this self-kicks you.
		if (npcId == byte.MaxValue || !NPCManager.Instance.NpcIsHoldingItem(npcId))
		{
			ChatManager.ChatMessage("NPC " + npcId + " isn't offering anything (sending this would get you kicked)");
			return;
		}
		Server.Instance.TakeItemFromNpc(Player.LocalPlayer, npcId);
		ChatManager.ChatMessage("Sent TakeItemFromNpc RPC for NPC ID: " + npcId);
	}

	// RPC #14 - SetItemMultiplier (HIGH: Unbounded multiplier -> infinite score)
	private static void UseSetItemMultiplierCommand(string[] args)
	{
		float mult = CommandCore.ParseFloat(args, 0, 1000000f);
		Item item = CommandCore.GetHeldItem();
		if (!item)
		{
			item = CommandCore.GetNearestItem(null);
		}
		if (!item)
		{
			ChatManager.ChatMessage("No item held or found to set multiplier");
			return;
		}
		// 遊戲只允許設定一次。Item.SetKillscoreMultiplier 的第二道守衛是
		//     if (this._killScoreMultiplier.Value != 1f) return;
		// 所以已經被改過的物品再送 RPC 也不會有任何反應——先講清楚，
		// 不要讓它看起來像成功了。
		if (Mathf.Abs(item.KillScoreMultiplier - 1f) > 0.0001f)
		{
			ChatManager.ChatMessage(item.name + " already has multiplier x" + item.KillScoreMultiplier
				+ " - the game only allows setting it once (SetKillscoreMultiplier guard)");
			return;
		}
		Server.Instance.SetItemMultiplier(item, mult);
		ChatManager.ChatMessage("Sent SetItemMultiplier RPC (" + mult + ") for item: " + item.name);
	}


	// RPC #16 - SendFinishGame (HIGH: Force finish game RPC)
	private static void UseSendFinishGameCommand(string[] args)
	{
		// Same as /finishgame: this ends the run for everyone and rolls the credits.
		if (!CommandCore.HasConfirmArg(args))
		{
			ChatManager.ChatMessage("<b>/sendfinishgame</b> ends the run for EVERYONE. Type <b>/sendfinishgame confirm</b> if you really mean it.");
			return;
		}
		if (OnlineIslandManager.CurIsland != 4)
		{
			ChatManager.ChatMessage("SendFinishGame is ignored by the server outside the final island");
			return;
		}
		Server.Instance.SendFinishGame();
		ChatManager.ChatMessage("Sent SendFinishGame RPC");
	}

	// ==========================================
	// MEDIUM ServerRPC Vulnerability Commands
	// ==========================================

	// RPC #17 - RemoveItemFromInventory (MEDIUM: Remove items from arbitrary player's inventory)
	private static void UseRemoveItemFromInventoryCommand(string[] args)
	{
		if (args.Length == 0)
		{
			ChatManager.ChatMessage("Usage: /removeplayeritem <playerIndex|name>");
			return;
		}
		Player target = CommandCore.ResolveSingleTarget(args[0], "/removeplayeritem");
		if (!target)
		{
			return;
		}
		Item item = (target.Holding ? target.Holding.HeldItem : null);
		if (!item && target.Inventory)
		{
			item = target.Inventory.SyncedCurItem;
		}
		if (item)
		{
			Server.Instance.RemoveItemFromInventory(target, item);
			ChatManager.ChatMessage("Sent RemoveItemFromInventory RPC for " + target.SteamName + " item: " + item.name);
		}
		else
		{
			ChatManager.ChatMessage(target.SteamName + " has no held item to remove");
		}
	}

	// RPC #18 - SetItemHolder (MEDIUM: Manipulate holder of item)
	private static void UseSetItemHolderCommand(string[] args)
	{
		Player newHolder = Player.LocalPlayer;
		if (args.Length > 0)
		{
			newHolder = CommandCore.ResolveSingleTarget(args[0], "/setitemholder");
			if (!newHolder)
			{
				return;
			}
		}
		Item targetItem = null;
		if (args.Length > 1)
		{
			Player fromPlayer = CommandCore.ResolveSingleTarget(args[1], "/setitemholder");
			if (!fromPlayer)
			{
				return;
			}
			targetItem = (fromPlayer.Holding ? fromPlayer.Holding.HeldItem : null);
			if (!targetItem)
			{
				ChatManager.ChatMessage(fromPlayer.SteamName + " isn't holding anything");
				return;
			}
		}
		// 沒指定來源玩家時，挑最近的「沒有人拿著的」物品。
		//
		// 舊版是挑其他玩家手上的物品——那正是伺服器唯一會拒絕的情況。
		// Server.cs 的 RpcLogic___SetItemHolder 裡有：
		//     if (syncedHolder && syncedHolder != A_2) { TargetReconcileRejectedItemPickup(...); return; }
		// 也就是「物品已經有持有者，而且不是要給的那個人」就直接退回。
		// 所以搶奪別人手上的東西是做不到的，這條指令實際能做的是
		// 把地上的東西「塞進」某人手裡。
		if (!targetItem)
		{
			targetItem = CommandCore.GetNearestItem((Item it) => !it.SyncedHolder);
		}
		if (!targetItem)
		{
			ChatManager.ChatMessage("No unheld item within " + CommandCore.MaxTargetRange + "m");
			return;
		}
		if (!newHolder)
		{
			ChatManager.ChatMessage("No target holder found");
			return;
		}
		if (targetItem.SyncedHolder && targetItem.SyncedHolder != newHolder)
		{
			ChatManager.ChatMessage(targetItem.name + " is held by " + targetItem.SyncedHolder.SteamName
				+ " - the server rejects taking items out of someone's hands (SetItemHolder guard)");
			return;
		}
		Server.Instance.SetItemHolder(targetItem, newHolder, null);
		ChatManager.ChatMessage("Sent SetItemHolder RPC: assigned " + targetItem.name + " to " + newHolder.SteamName);
	}

	// RPC #19 - SetSyncedSimulator (MEDIUM: Hijack item physics simulation authority)
	// 奪取物理模擬權。
	//
	// 舊版直接送 SetSyncedSimulator RPC，在「你就是房主」時等於什麼都沒做——
	// 你本來就是伺服器，SyncVar 設成自己的連線不會改變任何觀感。
	// 改走 RigidbodySync.StartSimulateLocal()，它是公開的，內部會處理
	// SetSyncedSimulator + ToggleSimulation，而且客戶端／房主兩邊都有效。
	private static void UseSetSyncedSimulatorCommand(string[] args)
	{
		if (!Player.LocalPlayer)
		{
			ChatManager.ChatMessage("Local player not ready");
			return;
		}
		List<Item> items = CommandCore.GetBatchItems(null);
		int count = 0;
		int already = 0;
		for (int i = 0; i < items.Count; i++)
		{
			RigidbodySync rs = items[i] ? items[i].RigidbodySync : null;
			if (!rs)
			{
				continue;
			}
			if (rs.SyncedSimulator == InstanceFinder.ClientManager.Connection)
			{
				already++;
				continue;
			}
			rs.StartSimulateLocal(default(Vector3), default(Quaternion));
			count++;
		}
		ChatManager.ChatMessage("Took over physics for " + count + " item(s)"
			+ ((already > 0) ? (" (" + already + " already simulated locally)") : ""));
	}

	// RPC #20 - UpdateItemPosRot (MEDIUM: Teleport/move items)
	private static void UseUpdateItemPosRotCommand(string[] args)
	{
		if (!Player.LocalPlayer || !Player.LocalPlayer.Transform)
		{
			ChatManager.ChatMessage("Local player not ready");
			return;
		}
		Vector3 targetPos = Player.LocalPlayer.Transform.position + Vector3.up * 0.5f;
		if (args.Length >= 3 && !CommandCore.TryParseVector3(args, 0, out targetPos))
		{
			ChatManager.ChatMessage("Couldn't parse coordinates (use '.' as the decimal separator)");
			return;
		}
		// 為什麼不送 UpdateItemPosRot RPC：
		// 伺服器端的 RigidbodySync.ServerSetPosRot 開頭是
		//     if (netCon != InstanceFinder.ClientManager.Connection) { ... }
		// 那是用來避免把位置回音給發送者的。正常流程（RigidbodySync.cs:730）送的
		// 就是自己的連線，所以在「你是房主」時這個條件永遠為假，整段被跳過，
		// 指令看起來完全沒反應。就算是純客戶端送成功了，真正在模擬那顆物體的人
		// 下一幀就會把位置蓋回去。
		//
		// 所以改成先接管模擬權再搬——這才是遊戲自己搬東西的做法。
		List<Item> items = CommandCore.GetBatchItems(null);
		int count = 0;
		int skipped = 0;
		for (int i = 0; i < items.Count; i++)
		{
			RigidbodySync rs = items[i] ? items[i].RigidbodySync : null;
			if (!rs)
			{
				skipped++;
				continue;
			}
			// StartSimulateLocal 在「已經是我們在模擬」時會提早 return 而不搬動，
			// 而且 pos 剛好是 Vector3.zero 時它也會略過定位，所以分開處理。
			if (rs.SyncedSimulator == InstanceFinder.ClientManager.Connection || targetPos == Vector3.zero)
			{
				rs.TeleportToPosRot(targetPos, Quaternion.identity, null, null);
			}
			else
			{
				rs.StartSimulateLocal(targetPos, Quaternion.identity);
			}
			count++;
		}
		ChatManager.ChatMessage("Moved " + count + " item(s) to " + targetPos
			+ ((skipped > 0) ? (" (" + skipped + " had no RigidbodySync)") : ""));
	}

	// RPC #21 - ResurrectPlayer (MEDIUM: Resurrect arbitrary dead player)
	private static void UseResurrectPlayerCommand(string[] args)
	{
		bool isAll = false;
		Player target = Player.LocalPlayer;
		if (args.Length > 0)
		{
			target = CommandCore.ResolvePlayerOrReport(args[0], out isAll);
			if (!target && !isAll)
			{
				return;
			}
		}

		if (isAll)
		{
			List<Player> targets = CommandCore.GetBatchPlayers(true);
			int count = 0;
			for (int i = 0; i < targets.Count; i++)
			{
				Player p = targets[i];
				if (p && p.Dying && p.Dying.DeadPlayer)
				{
					Server.Instance.ResurrectPlayer(p, p.Dying.DeadPlayer);
					count++;
				}
			}
			ChatManager.ChatMessage("Sent ResurrectPlayer RPC for " + count + " dead player(s)");
		}
		else if (target)
		{
			DeadPlayer dp2 = (target.Dying ? target.Dying.DeadPlayer : null);
			if (!dp2)
			{
				foreach (Item item in ItemManager.Items.Values)
				{
					DeadPlayer cand = item as DeadPlayer;
					if (cand && cand.Player == target)
					{
						dp2 = cand;
						break;
					}
				}
			}
			if (dp2)
			{
				Server.Instance.ResurrectPlayer(target, dp2);
				ChatManager.ChatMessage("Sent ResurrectPlayer RPC for " + target.SteamName);
			}
			else
			{
				ChatManager.ChatMessage("No DeadPlayer instance found for " + target.SteamName);
			}
		}
	}

	// RPC #22 - FinishEatingCreature (MEDIUM: Consume creature & heal player)
	private static void UseFinishEatingCreatureCommand(string[] args)
	{
		Player target = Player.LocalPlayer;
		if (args.Length > 0)
		{
			target = CommandCore.ResolveSingleTarget(args[0], "/forcefinisheating");
			if (!target)
			{
				return;
			}
		}
		Creature creature = CommandCore.GetTargetCreature();
		if (creature && target)
		{
			Server.Instance.FinishEatingCreature(creature, target);
			ChatManager.ChatMessage("Sent FinishEatingCreature RPC for " + target.SteamName);
		}
		else
		{
			ChatManager.ChatMessage("No creature within " + CommandCore.MaxTargetRange + "m, or player not found");
		}
	}

	// RPC #23 - SetDriver (MEDIUM: Set arbitrary player as boat driver)
	private static void UseSetDriverCommand(string[] args)
	{
		Player driver = Player.LocalPlayer;
		if (args.Length > 0)
		{
			if (args[0].Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				driver = null;
			}
			else
			{
				driver = CommandCore.ResolveSingleTarget(args[0], "/setboatdriver");
				if (!driver)
				{
					return;
				}
			}
		}
		Server.Instance.SetDriver(driver);
		ChatManager.ChatMessage("Sent SetDriver RPC for: " + (driver ? driver.SteamName : "None"));
	}

	// RPC #24 - SendBoatInput (MEDIUM: Steer boat without driver check)
	// 持續灌輸入用的狀態，由外面的 Update 每幀抽送（見 PumpBoatInput）。
	private static float _boatInputUntil;
	private static float _boatInputX;
	private static float _boatInputY;

	private static void UseSendBoatInputCommand(string[] args)
	{
		float x = CommandCore.ParseFloat(args, 0, 0f);
		float y = CommandCore.ParseFloat(args, 1, 1f);
		float seconds = Mathf.Clamp(CommandCore.ParseFloat(args, 2, 3f), 0f, 60f);

		// 伺服器端 Boat.ServerSetInput 開頭是
		//     if (!base.IsServerInitialized || !this._driver.Value) return;
		// 沒有駕駛就整段不做事，先講明白免得看起來像壞掉。
		if (!BoatManager.Boat)
		{
			ChatManager.ChatMessage("No boat in this level");
			return;
		}
		if (!BoatManager.Boat.Driver)
		{
			ChatManager.ChatMessage("The boat has no driver - the server ignores input entirely. "
				+ "Use /setboatdriver first.");
			return;
		}

		Server.Instance.SendBoatInput((half)x, (half)y);

		// 真正的駕駛每幀都在送自己的輸入（Boat.cs:721），單發會在下一幀被蓋掉，
		// 所以預設持續灌幾秒才看得出效果。
		if (seconds > 0f)
		{
			CommandCore._boatInputX = x;
			CommandCore._boatInputY = y;
			CommandCore._boatInputUntil = Time.time + seconds;
			ChatManager.ChatMessage("Overriding boat input (x=" + x + ", y=" + y + ") for " + seconds + "s");
		}
		else
		{
			ChatManager.ChatMessage("Sent one SendBoatInput RPC (x=" + x + ", y=" + y
				+ ") - the real driver will overwrite it next frame");
		}
	}

	/// <summary>每幀呼叫。持續期間內不斷重送操舵輸入，蓋過真正駕駛送的值。</summary>
	public static void PumpBoatInput()
	{
		if (CommandCore._boatInputUntil <= 0f)
		{
			return;
		}
		if (Time.time > CommandCore._boatInputUntil)
		{
			CommandCore._boatInputUntil = 0f;
			return;
		}
		if (!Server.Instance || !BoatManager.Boat || !BoatManager.Boat.Driver)
		{
			CommandCore._boatInputUntil = 0f;
			return;
		}
		Server.Instance.SendBoatInput((half)CommandCore._boatInputX, (half)CommandCore._boatInputY);
	}

	// RPC #25 - PlaceBet (MEDIUM: Place team bet on roulette color)
	private static void UsePlaceBetCommand(string[] args)
	{
		byte color = CommandCore.ParseByte(args, 0, 0);
		if (!CasinoManager.Instance)
		{
			ChatManager.ChatMessage("No casino here - the server would throw and kick you");
			return;
		}
		// 伺服器端：if (!CasinoManager.HasPlacedBet || CasinoManager.IsBetting) return;
		// HasPlacedBet 是「桌上已經放了值錢的東西」（_totalWorth > 0），不是「已經選了顏色」。
		if (!CasinoManager.HasPlacedBet)
		{
			ChatManager.ChatMessage("Nothing on the betting table yet - put an item on it first");
			return;
		}
		if (CasinoManager.IsBetting)
		{
			ChatManager.ChatMessage("A round is already spinning");
			return;
		}
		Server.Instance.PlaceBet(color);
		// 講清楚語意：這是「你押哪個顏色」，不是「開出哪個顏色」。
		// 伺服器只是把 _curBetColor 設成這個值然後開始轉，結果仍然是隨機的。
		ChatManager.ChatMessage("Started a round betting on colour index " + color
			+ " - this picks what YOU bet on, it does not decide the winning colour");
	}

	// RPC #26 - UpdateRoulette (MEDIUM: Spoof roulette ball/wheel visual desync)
	private static void UseUpdateRouletteCommand(string[] args)
	{
		float rot = CommandCore.ParseFloat(args, 0, 180f);
		if (!CasinoManager.Instance)
		{
			ChatManager.ChatMessage("No casino here - the server would throw and kick you");
			return;
		}
		Server.Instance.UpdateRoulette(Vector3.zero, rot);
		ChatManager.ChatMessage("Sent UpdateRoulette RPC with wheelRot: " + rot);
	}

	// RPC #27 & #28 - AddProjectile / AddProjectiles (MEDIUM: Spoof projectile origin)
	private static void UseSpoofProjectileCommand(string[] args)
	{
		Player owner = Player.LocalPlayer;
		if (args.Length > 0)
		{
			owner = CommandCore.ResolveSingleTarget(args[0], "/spoofprojectile");
			if (!owner)
			{
				return;
			}
		}
		int count = CommandCore.ParseInt(args, 1, 1);
		// Unbounded before: the velocity array is serialised into a single packet and
		// "new Vector3[count]" with a large count simply blew up the client.
		if (count < 1)
		{
			count = 1;
		}
		if (count > CommandCore.MaxSpoofedProjectiles)
		{
			ChatManager.ChatMessage("Capped projectile count at " + CommandCore.MaxSpoofedProjectiles);
			count = CommandCore.MaxSpoofedProjectiles;
		}
		WeaponInfo weaponInfo = new WeaponInfo
		{
			ProjectileType = 0,
			ProjectileDamage = 100,
			ProjectileForce = 50f,
			ProjectileGravity = 0f,
			ShootVFX = ""
		};
		if (!ProjectileManager.Instance)
		{
			ChatManager.ChatMessage("No ProjectileManager in this scene");
			return;
		}
		Transform cam = (Player.LocalPlayer ? Player.LocalPlayer.CamObject : null);
		Vector3 pos = (cam ? cam.position + cam.forward : Vector3.zero);
		Vector3 vel = (cam ? cam.forward * 50f : Vector3.forward * 50f);
		uint tick = InstanceFinder.TimeManager.Tick;

		if (count > 1)
		{
			Vector3[] vels = new Vector3[count];
			for (int i = 0; i < count; i++)
			{
				vels[i] = vel + UnityEngine.Random.insideUnitSphere * 2f;
			}
			Server.Instance.AddProjectiles(owner, weaponInfo, tick, 0U, pos, vels);
			ChatManager.ChatMessage("Sent AddProjectiles RPC (" + count + " projectiles) attributed to " + (owner ? owner.SteamName : "LocalPlayer"));
		}
		else
		{
			Server.Instance.AddProjectile(owner, weaponInfo, tick, 0U, pos, vel);
			ChatManager.ChatMessage("Sent AddProjectile RPC attributed to " + (owner ? owner.SteamName : "LocalPlayer"));
		}
	}

	// RPC #29 - GrillItemInLava (MEDIUM: Grill arbitrary item in lava)
	private static void UseGrillItemInLavaCommand()
	{
		Item item = CommandCore.GetHeldItem();
		if (!item)
		{
			item = CommandCore.GetNearestItem(null);
		}
		if (item)
		{
			Server.Instance.GrillItemInLava(item);
			ChatManager.ChatMessage("Sent GrillItemInLava RPC for item: " + item.name);
		}
		else
		{
			ChatManager.ChatMessage("No item held or found to grill in lava");
		}
	}

	// RPC #30 - SetIsAfk (MEDIUM: Mark arbitrary player AFK)
	private static void UseSetIsAfkCommand(string[] args)
	{
		if (args.Length == 0)
		{
			ChatManager.ChatMessage("Usage: /forcesetafk <playerIndex|name> [true|false]");
			return;
		}
		Player target = CommandCore.ResolveSingleTarget(args[0], "/forcesetafk");
		if (!target)
		{
			return;
		}
		bool isAfk = CommandCore.ParseBool(args, 1, true);
		Server.Instance.SetIsAfk(target, isAfk, false);
		ChatManager.ChatMessage("Sent SetIsAfk RPC (" + isAfk + ") for " + target.SteamName);
	}

	// RPC #31 - UnlockPocket (MEDIUM: Drain shared money for pockets)
	private static void UseUnlockPocketCommand(string[] args)
	{
		byte slotIndex = CommandCore.ParseByte(args, 0, 1);
		// PlayerInventory.GetExtraSlotCost does _extraSlotCosts[index - 1]; the subtraction
		// happens in int arithmetic, so index 0 reads [-1] and throws IndexOutOfRange on the
		// server, which makes FishNet kick you. The array has 5 entries, so 1-5 is the range.
		if (slotIndex < 1 || slotIndex > 5)
		{
			ChatManager.ChatMessage("Pocket slot index must be 1-5 (0 would get you kicked)");
			return;
		}
		Server.Instance.UnlockPocket(Player.LocalPlayer, slotIndex);
		ChatManager.ChatMessage("Sent UnlockPocket RPC for slot index: " + slotIndex);
	}

	// RPC #32 - BuyAttachment (MEDIUM: Drain shared money for weapon attachments)
	private static void UseBuyAttachmentCommand(string[] args)
	{
		byte attachmentIndex = CommandCore.ParseByte(args, 0, 0);
		Item item = CommandCore.GetHeldItem();
		Weapon weapon = (item ? item.Weapon : null);
		if (!weapon)
		{
			Item nearest = CommandCore.GetNearestItem((Item it) => it.Weapon);
			weapon = (nearest ? nearest.Weapon : null);
		}
		if (weapon)
		{
			Server.Instance.BuyAttachment(weapon, attachmentIndex);
			ChatManager.ChatMessage("Sent BuyAttachment RPC for attachment index: " + attachmentIndex);
		}
		else
		{
			ChatManager.ChatMessage("No weapon found or held to buy attachment");
		}
	}

	// RPC #33 - BuyBulletUpgrade (MEDIUM: Drain shared money for bullet upgrade)
	private static void UseBuyBulletUpgradeCommand()
	{
		Item item = CommandCore.GetHeldItem();
		Weapon weapon = (item ? item.Weapon : null);
		if (!weapon)
		{
			Item nearest = CommandCore.GetNearestItem((Item it) => it.Weapon);
			weapon = (nearest ? nearest.Weapon : null);
		}
		if (weapon)
		{
			Server.Instance.BuyBulletUpgrade(weapon);
			ChatManager.ChatMessage("Sent BuyBulletUpgrade RPC");
		}
		else
		{
			ChatManager.ChatMessage("No weapon found or held to buy bullet upgrade");
		}
	}

	// RPC #34 - BuySharpnessUpgrade (MEDIUM: Drain shared money for sharpness upgrade)
	private static void UseBuySharpnessUpgradeCommand()
	{
		Item item = CommandCore.GetHeldItem();
		Melee melee = (item ? item.Melee : null);
		if (!melee)
		{
			Item nearest = CommandCore.GetNearestItem((Item it) => it.Melee);
			melee = (nearest ? nearest.Melee : null);
		}
		if (melee)
		{
			Server.Instance.BuySharpnessUpgrade(melee);
			ChatManager.ChatMessage("Sent BuySharpnessUpgrade RPC");
		}
		else
		{
			ChatManager.ChatMessage("No melee weapon found or held to buy sharpness upgrade");
		}
	}

	// RPC #35 - ProjectileHitDynamic (MEDIUM: Spoof projectile hit attribution)
	private static void UseProjectileHitDynamicCommand(string[] args)
	{
		uint id = CommandCore.ParseUInt(args, 0, 0U);
		Player target = Player.LocalPlayer;
		if (args.Length > 1)
		{
			target = CommandCore.ResolveSingleTarget(args[1], "/spoofprojectilehit");
			if (!target)
			{
				return;
			}
		}
		if (!target)
		{
			ChatManager.ChatMessage("Local player not ready");
			return;
		}
		if (!ProjectileManager.Instance)
		{
			ChatManager.ChatMessage("No ProjectileManager in this scene");
			return;
		}
		Server.Instance.ProjectileHitDynamic(target.Owner, id);
		ChatManager.ChatMessage("Sent ProjectileHitDynamic RPC for ID: " + id);
	}

	// Help list for ServerRPC vulnerability commands
	private static void UseVulnHelpCommand()
	{
		ChatManager.ChatMessage("<b>[CRITICAL]</b> /buyfree, /hitcreature, /hitplayer");
		ChatManager.ChatMessage("<b>[HIGH]</b> /buybaitfree, /buymotorfree, /buyradarfree, /forcerespawn, /tpplayer, /forceplayerpos, /forcedropall, /detonateexplosives, /takenpcitem, /setitemmultiplier, /sendfinishgame");
		ChatManager.ChatMessage("<b>[MEDIUM]</b> /removeplayeritem, /setitemholder, /hijackitemphysics, /tpitems, /forceresurrect, /forcefinisheating, /setboatdriver, /steerboat, /forceplacebet, /spoofroulette, /spoofprojectile, /grillhelditem, /forcesetafk, /forceunlockpocket, /forcebuyattachment, /forcebuybulletupgrade, /forcebuysharpnessupgrade, /spoofprojectilehit");
		ChatManager.ChatMessage("<b>[GAME]</b> /spawn, /spawndead, /spawndrip, /spawndripdead, /grill, /boat, /addmoney, /removemoney, /nextisland, /previsland, /godmode, /oneshot, /killallcreatures, /killalldripcreatures, /resetallcreatures, /resetalldripcreatures, /slots, /killboss, /allskins, /noskins, /showkillscores, /unlockachievements, /lockachievements");
		ChatManager.ChatMessage("<b>[ENDS THE RUN - needs 'confirm']</b> /finishgame confirm, /sendfinishgame confirm");
		ChatManager.ChatMessage("Targets accept: <b>me</b>, <b>all</b>, a player index, a Steam name or a SteamID. Batch commands are capped at " + CommandCore.MaxBatchTargets + " targets.");
	}

	private const string SpawnCommand = "spawn";

	private const string SpawnDeadCommand = "spawndead";

	private const string SpawnDripCommand = "spawndrip";

	private const string SpawnDeadDripCommand = "spawndripdead";

	private const string GrillCommand = "grill";

	private const string BoatCommand = "boat";

	private const string GiveCommand = "addmoney";

	private const string RemoveCommand = "removemoney";

	private const string NextIslandCommand = "nextisland";

	private const string PrevIslandCommand = "previsland";

	private const string GodModeCommand = "godmode";

	private const string OneShotCommand = "oneshot";

	private const string KillAllCreaturesCommand = "killallcreatures";

	private const string KillAllDripCreaturesCommand = "killalldripcreatures";

	private const string ResetAllCreaturesCommand = "resetallcreatures";

	private const string ResetAllDripCreaturesCommand = "resetalldripcreatures";

	private const string SlotsCommand = "slots";

	private const string KillBossCommand = "killboss";

	private const string UnlockAllSkinsCommand = "allskins";

	private const string LockAllSkinsCommand = "noskins";

	private const string ShowKillscoresCommand = "showkillscores";

	private const string FinishGameCommand = "finishgame";

	private const string UnlockAchievementsCommand = "unlockachievements";

	private const string LockAchievementsCommand = "lockachievements";
}
}

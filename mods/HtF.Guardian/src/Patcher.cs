using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FishNet.Connection;
using HarmonyLib;

namespace HtF.Guardian
{
    /// <summary>
    /// 把守衛掛上去。
    ///
    /// **目標一律用前綴比對找，不寫死方法名。** FishNet 的 weaver 產生的方法叫
    /// <c>RpcLogic___HitCreature___215526726</c>，後面那串是雜湊，遊戲改一次
    /// 參數就會變。用 <c>"RpcLogic___HitCreature___"</c> 當前綴找，雜湊變了照樣找得到；
    /// 真的整個不見了（改名／移除）就在啟動時留一行警告，不會默默失效。
    ///
    /// 掛的東西有兩種：
    ///
    /// 1. **所有 reader** 一個共用的 prefix／postfix，把 FishNet 給的發送端連線
    ///    存進 <see cref="Sender"/>。注意 <c>TargetRpc</c> 的 reader 只有兩個參數
    ///    （沒有 conn），必須濾掉，不然 Harmony 綁 <c>__2</c> 會在 patch 當下就爆。
    /// 2. **下表列出的 logic** 各自的守衛。
    /// </summary>
    internal static class Patcher
    {
        /// <summary>RPC 名稱 → <see cref="Guards"/> 裡的守衛方法名。</summary>
        private static readonly string[,] Table =
        {
            // 玩家身分
            { "SpawnPlayer",                      "SpawnPlayer" },
            { "RespawnPlayer",                    "RespawnPlayer" },
            { "TeleportPlayer",                   "TeleportPlayer" },
            { "UpdatePlayerPosRot",               "UpdatePlayerPosRot" },
            { "UpdatePlayerCrouching",            "UpdatePlayerCrouching" },
            { "SetIsAfk",                         "SetIsAfk" },
            { "SendFinishedTutorial",             "SendFinishedTutorial" },
            { "ResurrectPlayer",                  "ResurrectPlayer" },
            { "UpdateDeadPlayerResurrectPercent", "UpdateDeadPlayerResurrectPercent" },

            // 傷害
            { "HitCreature",                      "HitCreature" },
            { "HitPlayer",                        "HitPlayer" },
            { "ActivateExplosive",                "ActivateExplosive" },
            { "Punch",                            "Punch" },
            { "MeleeAttack",                      "MeleeAttack" },

            // 武器與彈道
            { "AddProjectile",                    "AddProjectile" },
            { "AddProjectiles",                   "AddProjectiles" },
            { "ProjectileHitDynamic",             "ProjectileHitDynamic" },
            { "ReloadWeapon",                     "ReloadWeapon" },
            { "InspectTool",                      "InspectTool" },
            { "UpdateHeldToolPosRot",             "UpdateHeldToolPosRot" },

            // 物品
            { "SetItemHolder",                    "SetItemHolder" },
            { "PutItemInInventory",               "PutItemInInventory" },
            { "RemoveItemFromInventory",          "RemoveItemFromInventory" },
            { "DropAllItems",                     "DropAllItems" },
            { "SelectInvSlot",                    "SelectInvSlot" },
            { "SetItemSkin",                      "SetItemSkin" },
            { "SetItemMultiplier",                "SetItemMultiplier" },
            { "GrillItemInLava",                  "GrillItemInLava" },
            { "PlayImpactSound",                  "PlayImpactSound" },
            { "HandOverItemSimulation",           "HandOverItemSimulation" },
            { "UpdateItemPosRot",                 "UpdateItemPosRot" },
            { "SetSyncedSimulator",               "SetSyncedSimulator" },

            // 釣魚
            { "UpdateBaitPosAndLineLength",       "UpdateBaitPosAndLineLength" },
            { "UpdateRodPullBack",                "UpdateRodPullBack" },
            { "ReleaseItemFromBait",              "ReleaseItemFromBait" },
            { "ChangeBait",                       "ChangeBait" },

            // 經濟
            { "BuyItem",                          "BuyItem" },
            { "UnlockPocket",                     "UnlockPocket" },
            { "BuyBait",                          "BuyBait" },
            { "BuyBoatMotor",                     "BuyBoatMotor" },
            { "BuyBoatRadar",                     "BuyBoatRadar" },
            { "BuyAttachment",                    "BuyAttachment" },
            { "BuyBulletUpgrade",                 "BuyBulletUpgrade" },
            { "BuySharpnessUpgrade",              "BuySharpnessUpgrade" },
            { "TakeItemFromNpc",                  "TakeItemFromNpc" },

            // 賭場
            { "PlaceBet",                         "PlaceBet" },
            { "UpdateRoulette",                   "UpdateRoulette" },

            // 船
            { "SetDriver",                        "SetDriver" },
            { "SendBoatInput",                    "SendBoatInput" },
            { "SetBoatSkin",                      "SetBoatSkin" },

            // 其他
            { "SendChatMessage",                  "SendChatMessage" },
            { "FinishEatingCreature",             "FinishEatingCreature" },
            { "ToggleEatCreature",                "ToggleEatCreature" },
            { "SetRadioFrequency",                "SetRadioFrequency" },
            { "ClientSpokeToNpc",                 "ClientSpokeToNpc" },
            { "SendFinishGame",                   "SendFinishGame" },
        };

        /// <summary>掛上去的 RPC 名稱，面板顯示涵蓋率用。</summary>
        internal static readonly List<string> Guarded = new List<string>();

        /// <summary>沒掛成的，啟動時已經警告過，面板也看得到。</summary>
        internal static readonly List<string> Missing = new List<string>();

        internal static int ReadersPatched;
        internal static int ServerRpcTotal;

        private const BindingFlags Any =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Apply(Harmony harmony)
        {
            MethodInfo[] all = typeof(Server).GetMethods(Any);

            PatchReaders(harmony, all);
            PatchLogic(harmony, all);
            PatchUi(harmony);
            CountAndReport(all);
        }

        /// <summary>面板開著的時候擋掉本機輸入。跟守衛無關，只是介面禮貌。</summary>
        private static void PatchUi(Harmony harmony)
        {
            try
            {
                MethodInfo getter = AccessTools.PropertyGetter(typeof(Player), nameof(Player.BlockInputs));
                if (getter == null)
                {
                    Plugin.Log.LogWarning("找不到 Player.BlockInputs，面板開著時仍然會操作到角色。");
                    return;
                }
                harmony.Patch(getter, null, new HarmonyMethod(AccessTools.Method(typeof(Guards), nameof(Guards.BlockInputs))));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("掛 Player.BlockInputs 失敗：" + e.Message);
            }
        }

        // ------------------------------------------------------------------ reader

        private static void PatchReaders(Harmony harmony, MethodInfo[] all)
        {
            var pre = new HarmonyMethod(AccessTools.Method(typeof(Guards), nameof(Guards.ReaderPrefix)));
            var fin = new HarmonyMethod(AccessTools.Method(typeof(Guards), nameof(Guards.ReaderFinalizer)));

            foreach (MethodInfo m in all)
            {
                if (!m.Name.StartsWith("RpcReader___", StringComparison.Ordinal)) continue;

                // ServerRpc 的 reader 是 (PooledReader, Channel, NetworkConnection)；
                // TargetRpc／ObserversRpc 的只有前兩個。綁 __2 在那些上面會在
                // patch 當下就丟例外，所以先濾掉。
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 3 || ps[2].ParameterType != typeof(NetworkConnection)) continue;

                try
                {
                    // 具名參數：Patch 的第三個位置參數是 postfix，而這裡要的是
                    // finalizer——原方法丟例外時 postfix 不會跑，Sender 會漏放。
                    harmony.Patch(m, prefix: pre, finalizer: fin);
                    ReadersPatched++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("掛 " + m.Name + " 失敗：" + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------ logic

        private static void PatchLogic(Harmony harmony, MethodInfo[] all)
        {
            for (int i = 0; i < Table.GetLength(0); i++)
            {
                string rpc = Table[i, 0];
                string guardName = Table[i, 1];

                MethodInfo target = FindLogic(all, rpc);
                if (target == null)
                {
                    Missing.Add(rpc);
                    Plugin.Log.LogWarning("找不到 Server.RpcLogic___" + rpc + "___*（遊戲更新？）——這條 RPC 沒有守衛。");
                    continue;
                }

                MethodInfo guard = AccessTools.Method(typeof(Guards), guardName);
                if (guard == null)
                {
                    Missing.Add(rpc);
                    Plugin.Log.LogError("Guards." + guardName + " 不存在，" + rpc + " 沒有守衛。");
                    continue;
                }

                try
                {
                    harmony.Patch(target, new HarmonyMethod(guard));
                    Guarded.Add(rpc);
                }
                catch (Exception e)
                {
                    // 最可能的原因是遊戲改了參數順序／型別，讓 __N 對不上。
                    Missing.Add(rpc);
                    Plugin.Log.LogError("掛 " + rpc + " 的守衛失敗（參數對不上？）：" + e.Message);
                }
            }
        }

        private static MethodInfo FindLogic(MethodInfo[] all, string rpc)
        {
            string prefix = "RpcLogic___" + rpc + "___";
            foreach (MethodInfo m in all)
                if (m.Name.StartsWith(prefix, StringComparison.Ordinal)) return m;
            return null;
        }

        // ------------------------------------------------------------------ 涵蓋率

        /// <summary>
        /// 啟動時把涵蓋率寫進 log：掛了幾條、遊戲總共有幾條、哪幾條刻意沒守衛。
        /// 遊戲更新加了新的 ServerRpc 時，這一行就是唯一的提醒。
        /// </summary>
        private static void CountAndReport(MethodInfo[] all)
        {
            var names = new List<string>();
            foreach (MethodInfo m in all)
            {
                if (!m.Name.StartsWith("RpcLogic___", StringComparison.Ordinal)) continue;
                ParameterInfo[] ps = m.GetParameters();
                // TargetRpc 的 logic 第一個參數是收件者連線，不是我們要管的。
                if (ps.Length > 0 && ps[0].ParameterType == typeof(NetworkConnection)
                    && m.Name.StartsWith("RpcLogic___Target", StringComparison.Ordinal)) continue;

                int cut = m.Name.LastIndexOf("___", StringComparison.Ordinal);
                names.Add(cut > 11 ? m.Name.Substring(11, cut - 11) : m.Name);
            }
            ServerRpcTotal = names.Count;

            var unguarded = new List<string>();
            foreach (string n in names)
                if (!Guarded.Contains(n)) unguarded.Add(n);

            Plugin.Log.LogInfo(string.Format("守衛 {0} / {1} 條 RPC，reader {2} 條。",
                Guarded.Count, ServerRpcTotal, ReadersPatched));

            if (unguarded.Count > 0)
            {
                var sb = new StringBuilder("沒有守衛的：");
                for (int i = 0; i < unguarded.Count; i++)
                {
                    if (i > 0) sb.Append('、');
                    sb.Append(unguarded[i]);
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
        }
    }
}

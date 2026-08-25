using System.Collections.Generic;
using HarmonyLib;

namespace HtF.FishingEcology
{
    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>
        /// 抽魚前把權重表換成調整過的副本。
        ///
        /// 用 prefix 換掉 ref 參數、而不是自己重寫抽取邏輯，是刻意的：
        /// 原方法裡有「場上已有 Boss 就退回 _defaultFishable」這條規則，
        /// 還有權重跑完沒中的保險，照著抄一遍只會多出對不上的風險。
        /// </summary>
        [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.GetRandomItem))]
        [HarmonyPrefix]
        private static void GetRandomItem_Prefix(ref List<ItemInfoWeight> weights)
        {
            if (!Plugin.Enabled.Value) return;

            List<ItemInfoWeight> adjusted = WeightTable.Build(weights);
            if (adjusted != null) weights = adjusted;
        }

        /// <summary>看抽到的是不是稀有，維護保底計數。</summary>
        [HarmonyPatch(typeof(CreatureManager), nameof(CreatureManager.GetRandomItem))]
        [HarmonyPostfix]
        private static void GetRandomItem_Postfix(Fishable __result)
        {
            if (!Plugin.Enabled.Value) return;

            bool rare = __result != null && WeightTable.LastRareSet.Contains(__result);
            if (rare) WeightTable.MissStreak = 0;
            else WeightTable.MissStreak++;

            if (Plugin.LogRolls.Value)
            {
                string name = (__result != null && __result.ItemToSpawn)
                    ? __result.ItemToSpawn.name : "(null)";
                Plugin.Log.LogInfo("抽到 " + name + (rare ? "（稀有）" : "") + "　連續非稀有 " + WeightTable.MissStreak);
            }
        }
    }
}

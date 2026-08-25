using System.Reflection;
using HarmonyLib;

namespace HtF.HostRules
{
    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class Patches
    {
        internal const string OnDifficultyChangeName = "OnDifficultyChange";

        /// <summary>
        /// 伺服器起來時套用一次。
        /// ServerSettings.OnStartServer 會把難度 SyncVar 設成存檔裡的值，
        /// 我們接在後面覆蓋乘數、並把規則開關調成設定要的狀態。
        /// </summary>
        [HarmonyPatch(typeof(ServerSettings), nameof(ServerSettings.OnStartServer))]
        [HarmonyPostfix]
        private static void ServerSettings_OnStartServer_Postfix()
        {
            RuleApplier.ApplyMultipliers();
            RuleApplier.ApplyToggles();
        }

        /// <summary>
        /// 難度變動（房主在暫停選單改難度）之後，遊戲會把乘數設回三段式的固定值，
        /// 所以必須在這裡再蓋一次，否則覆寫會被洗掉。
        /// 這是私有方法，只能用字串指定；Plugin.Awake 有做存在性檢查。
        /// </summary>
        [HarmonyPatch(typeof(ServerSettings), OnDifficultyChangeName)]
        [HarmonyPostfix]
        private static void OnDifficultyChange_Postfix()
        {
            RuleApplier.ApplyMultipliers();
        }

        /// <summary>
        /// 每位玩家在伺服器上初始化時套用數值調整。
        /// OnStartServer 正是把 TickUpdate 掛上 TimeManager.OnTick 的地方，
        /// 那些飽食／回血／中毒／著火的欄位都只在伺服器端被讀。
        /// </summary>
        [HarmonyPatch(typeof(PlayerVitals), nameof(PlayerVitals.OnStartServer))]
        [HarmonyPostfix]
        private static void PlayerVitals_OnStartServer_Postfix(PlayerVitals __instance)
        {
            VitalsTuner.Apply(__instance);
        }

        /// <summary>啟動時確認字串指定的目標還在，遊戲更新後改名才不會默默失效。</summary>
        internal static void VerifyTargets()
        {
            MethodInfo m = AccessTools.Method(typeof(ServerSettings), OnDifficultyChangeName);
            if (m == null)
                Plugin.Log.LogWarning("ServerSettings." + OnDifficultyChangeName
                    + " 不存在了（遊戲更新？）——房主在遊戲中改難度時，覆寫的乘數會被遊戲蓋回去。");
        }
    }
}

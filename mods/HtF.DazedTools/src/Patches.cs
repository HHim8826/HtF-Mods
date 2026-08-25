using HtF.DazedTools.Commands;
using HtF.DazedTools.UI;
using HarmonyLib;

namespace HtF.DazedTools
{
    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>
        /// 聊天欄輸入的指令改走我們修好的版本。
        /// 遊戲原本的 DazedCommands 有幾個必定自踢的 bug（見 SECURITY_AUDIT_ServerRPC.md），
        /// 所以這裡直接跳過原實作。
        /// </summary>
        [HarmonyPatch(typeof(global::DazedCommands), nameof(global::DazedCommands.IsServerCommand))]
        [HarmonyPrefix]
        private static bool IsServerCommand_Prefix(string fullCommand, ref bool __result)
        {
            __result = CommandCore.IsServerCommand(fullCommand);
            return false; // 不執行原方法
        }

        /// <summary>視窗開著的時候把本機玩家的輸入全部擋掉（移動、視角、開火、拿取…）。</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.BlockInputs), MethodType.Getter)]
        [HarmonyPostfix]
        private static void BlockInputs_Postfix(Player __instance, ref bool __result)
        {
            if (ModWindow.Visible && __instance == Player.LocalPlayer) __result = true;
        }

        /// <summary>把遊戲的聊天輸出鏡射到視窗下方的輸出區。</summary>
        [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.ChatMessage), new[] { typeof(string) })]
        [HarmonyPostfix]
        private static void ChatMessage_Postfix(string message)
        {
            ModWindow.PushLog(message);
        }

        /// <summary>選用：一併打開遊戲自己的作弊旗標（M/N/O/逗號 等內建熱鍵）。</summary>
        [HarmonyPatch(typeof(ClientSettings), nameof(ClientSettings.CheatsEnabled), MethodType.Getter)]
        [HarmonyPostfix]
        private static void CheatsEnabled_Postfix(ref bool __result)
        {
            if (Plugin.ForceGameCheatFlag != null && Plugin.ForceGameCheatFlag.Value) __result = true;
        }
    }
}

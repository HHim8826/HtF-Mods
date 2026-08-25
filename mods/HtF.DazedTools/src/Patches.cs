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

        // 「一併打開遊戲自己的作弊旗標」以前是 patch ClientSettings.CheatsEnabled 的 getter，
        // 那是無效的：它是一行的 auto-property（ClientSettings.cs:10），Mono 會把 getter
        // inline 進呼叫端（MoneyManager.Update 等），patch 上去**靜靜地不生效**——
        // 沒有錯誤、沒有 log，只是 M/N/O 熱鍵不會動。這正是 MODDING_CONTEXT 第 3.2 節
        // 記下來的那條規則，這裡自己踩了。
        //
        // 改成呼叫公開的 setter ClientSettings.ToggleCheats(bool)，見 Plugin.ApplyCheatFlag。
    }
}

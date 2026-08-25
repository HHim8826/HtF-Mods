using HtF.Shared;

namespace HtF.DazedTools.UI
{
    /// <summary>
    /// 視窗上的文字。底層是共用的 <see cref="Loc"/>（`mods/Shared/Loc.cs`）：
    /// 語言跟著 ConfigMenu 的「語言」設定走，沒裝 ConfigMenu 就跟著遊戲語系。
    ///
    /// 指令本身的說明不在這裡，在 <see cref="Registry"/>——那裡每個指令的中英文
    /// 就寫在同一行上，看得到彼此。設定項的說明則在 `Plugin.Awake`。
    ///
    /// **送出去的指令字串永遠不經過這裡。** `/spawn tuna`、`me`、`all`、`confirm`
    /// 這些是遊戲的指令語法，不是給人看的文字，翻譯它們等於送出壞掉的指令。
    /// </summary>
    internal static class L
    {
        internal static string DangerUnlocked { get { return Loc.P("危險指令：已解鎖", "Danger: unlocked"); } }
        internal static string DangerLocked { get { return Loc.P("危險指令：已上鎖", "Danger: locked"); } }
        internal static string DangerUnlockedLog { get { return Loc.P("[Dazed] 危險指令已解鎖", "[Dazed] Danger commands unlocked"); } }
        internal static string DangerLockedLog { get { return Loc.P("[Dazed] 危險指令已上鎖", "[Dazed] Danger commands locked"); } }

        internal static string Close { get { return Loc.P("關閉", "Close"); } }
        internal static string Cancel { get { return Loc.P("取消", "Cancel"); } }
        internal static string Search { get { return Loc.P("搜尋", "Search"); } }
        internal static string Clear { get { return Loc.P("清除", "Clear"); } }
        internal static string Output { get { return Loc.P("輸出", "Output"); } }
        internal static string Console { get { return Loc.P("指令", "Command"); } }
        internal static string Send { get { return Loc.P("送出", "Send"); } }

        internal static string Run { get { return Loc.P("執行", "Run"); } }
        internal static string Locked { get { return Loc.P("已鎖定", "Locked"); } }

        internal static string On { get { return Loc.P("開", "On"); } }
        internal static string Off { get { return Loc.P("關", "Off"); } }

        internal static string MyPosition { get { return Loc.P("我的位置", "My position"); } }
        internal static string Coords { get { return Loc.P("座標", "Coords"); } }
        internal static string Void { get { return Loc.P("虛空", "Void"); } }

        internal static string Me { get { return Loc.P("我自己", "Me"); } }
        internal static string AllPlayers { get { return Loc.P("全部玩家", "All players"); } }
        internal static string AllPlayersCapped { get { return Loc.P("全部玩家（上限 48）", "All players (max 48)"); } }
        internal static string NoDriver { get { return Loc.P("沒有駕駛", "No driver"); } }
        internal static string NotChosen { get { return Loc.P("（未選）", "(none)"); } }
        internal static string NoMatches { get { return Loc.P("沒有符合的項目", "No matching entries"); } }

        internal static string Connected { get { return Loc.P("● 已連線", "● Connected"); } }
        internal static string Disconnected { get { return Loc.P("○ 未連線", "○ Not connected"); } }
        internal static string NoLocalPlayer { get { return Loc.P("(無本機玩家)", "(no local player)"); } }
        internal static string StatusUnavailable { get { return Loc.P("狀態不可用", "Status unavailable"); } }

        /// <summary>「● 已連線    玩家 3 人    你是 Leo」／「● Connected    3 players    you are Leo」</summary>
        internal static string StatusLine(string conn, int players, string me)
        {
            return Loc.IsEnglish
                ? conn + "    " + players + " players    you are " + me
                : conn + "    玩家 " + players + " 人    你是 " + me;
        }

        internal static string PickTitle(string argLabel)
        {
            return Loc.P("選擇 " + argLabel, "Select " + argLabel);
        }

        internal static string CommandThrew { get { return Loc.P("指令丟出例外：", "The command threw an exception: "); } }
    }
}

using HtF.Shared;

namespace HtF.Guardian
{
    /// <summary>
    /// 面板與提示的文字。底層是共用的 <see cref="Loc"/>（`mods/Shared/Loc.cs`）：
    /// 語言跟著 ConfigMenu 的「語言」設定走，沒裝 ConfigMenu 就跟著遊戲語系。
    ///
    /// 設定項的英文名稱與說明不在這裡——它們寫在 `Plugin.Awake` 的
    /// <see cref="Loc.Bind{T}"/> 呼叫裡，一個設定一行。
    /// </summary>
    internal static class L
    {
        internal static string Title { get { return Loc.P("GUARDIAN 監控", "GUARDIAN MONITOR"); } }

        internal static string TabPlayers { get { return Loc.P("連線", "Connections"); } }
        internal static string TabEvents { get { return Loc.P("事件", "Events"); } }
        internal static string TabBans { get { return Loc.P("封鎖名單", "Ban List"); } }

        internal static string NotHosting
        {
            get
            {
                return Loc.P("你不是房主，這個 mod 現在沒有作用。\n所有守衛都在伺服器端，開房之後才會啟動。",
                             "You are not hosting, so this mod is doing nothing right now.\n"
                             + "Every guard lives on the server side and starts once you host a lobby.");
            }
        }

        internal static string Disabled
        {
            get { return Loc.P("總開關是關的（設定 → 一般 → 啟用）。", "The master switch is off (Settings → General → Enabled)."); }
        }

        internal static string NoPlayers { get { return Loc.P("目前沒有其他人連進來。", "Nobody else is connected."); } }
        internal static string NoEvents { get { return Loc.P("還沒有擋下任何東西。", "Nothing has been blocked yet."); } }
        internal static string NoBans { get { return Loc.P("封鎖名單是空的。", "The ban list is empty."); } }

        internal static string ColPlayer { get { return Loc.P("玩家", "Player"); } }
        internal static string ColViolations { get { return Loc.P("違規", "Blocked"); } }
        internal static string ColRateDrops { get { return Loc.P("丟包", "Dropped"); } }
        internal static string ColLast { get { return Loc.P("最後一次", "Last"); } }
        internal static string ColAction { get { return Loc.P("動作", "Actions"); } }
        internal static string ColSteamId { get { return Loc.P("Steam ID", "Steam ID"); } }

        internal static string Kick { get { return Loc.P("踢出", "Kick"); } }
        internal static string Ban { get { return Loc.P("封鎖", "Ban"); } }
        internal static string Unban { get { return Loc.P("解除", "Unban"); } }
        internal static string ResetStats { get { return Loc.P("清除統計", "Reset counters"); } }
        internal static string ReloadBans { get { return Loc.P("重新載入名單", "Reload list"); } }
        internal static string OpenFolder { get { return Loc.P("開啟資料夾", "Open folder"); } }
        internal static string Close { get { return Loc.P("關閉", "Close"); } }
        internal static string Host { get { return Loc.P("（房主）", "(host)"); } }

        internal static string Coverage(int guarded, int total, int readers)
        {
            return Loc.P(
                string.Format("守衛 {0} / {1} 條 ServerRpc，reader {2} 條", guarded, total, readers),
                string.Format("Guarding {0} of {1} ServerRpcs, {2} readers hooked", guarded, total, readers));
        }

        internal static string Blocked(int n)
        {
            return Loc.P("已擋下 " + n + " 次", n + " blocked");
        }

        internal static string MissingTargets(int n)
        {
            return Loc.P("有 " + n + " 條 RPC 掛不上（看 log）",
                         n + " RPCs could not be hooked (see the log)");
        }

        internal static string SecondsAgo(float s)
        {
            int v = (int)s;
            return Loc.P(v + " 秒前", v + "s ago");
        }

        internal static string AnnounceKicked(string name)
        {
            return Loc.P("已踢出 " + name + "：違規次數超過上限。",
                         "Kicked " + name + ": too many rejected packets.");
        }

        internal static string AnnounceBanned(string name)
        {
            return Loc.P("已封鎖並踢出 " + name + "。",
                         "Banned and kicked " + name + ".");
        }

        internal static string AnnounceBannedJoin(string who)
        {
            return Loc.P("擋下封鎖名單上的連線：" + who,
                         "Rejected a connection on the ban list: " + who);
        }

        /// <summary>擋下來的原因。列舉成員名本身就是中文，英文另外查表。</summary>
        internal static string WhyText(Why why)
        {
            if (!Loc.IsEnglish) return why.ToString();
            switch (why)
            {
                case Why.身分不符: return "Wrong actor";
                case Why.不是持有者: return "Not the holder";
                case Why.不是模擬者: return "Not the simulator";
                case Why.不是駕駛: return "Not the driver";
                case Why.數值超出範圍: return "Value out of range";
                case Why.移動過快: return "Moving too fast";
                case Why.索引超出範圍: return "Index out of range";
                case Why.價格不符: return "Wrong price";
                case Why.冒用身分: return "Impersonation";
                case Why.太頻繁: return "Rate limited";
                case Why.重複生成: return "Duplicate spawn";
                case Why.已封鎖: return "Banned";
                case Why.限房主: return "Host only";
                case Why.目標無效: return "Invalid target";
                default: return why.ToString();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HtF.Guardian
{
    /// <summary>
    /// 封鎖名單，存成一個純文字檔，一行一個 Steam ID。
    ///
    /// 為什麼要有這個：FishNet 的 <c>Kick</c> 只是把連線斷掉，對方可以立刻再連進來。
    /// 名單在 <see cref="Watcher"/> 收到連線建立時比對，第一時間就踢掉。
    ///
    /// 比對用的 ID 來自 <c>NetworkConnection.GetAddress()</c>（傳輸層給的），
    /// 不是客戶端在 RPC 參數裡自填的那個——那個能偽造。
    /// </summary>
    internal static class Bans
    {
        private const string FileName = "banned.txt";

        /// <summary>SteamID → 加入名單時記下的名字（只是給人看的註解）。</summary>
        internal static readonly Dictionary<ulong, string> Banned = new Dictionary<ulong, string>();

        internal static string Folder
        {
            get { return Path.Combine(BepInEx.Paths.ConfigPath, "HtF.Guardian"); }
        }

        internal static string FilePath { get { return Path.Combine(Folder, FileName); } }

        internal static void Load()
        {
            Banned.Clear();
            try
            {
                if (!File.Exists(FilePath))
                {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(FilePath, Header(), new UTF8Encoding(false));
                    return;
                }

                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    // 「17位數字  # 名字」——名字只是註解，切在第一個空白或 # 上。
                    int cut = line.IndexOfAny(new[] { ' ', '\t', '#' });
                    string idPart = cut < 0 ? line : line.Substring(0, cut).Trim();
                    string note = cut < 0 ? "" : line.Substring(cut).Trim(' ', '\t', '#');

                    ulong id;
                    if (ulong.TryParse(idPart, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0UL)
                        Banned[id] = note;
                }

                if (Banned.Count > 0) Plugin.Log.LogInfo("封鎖名單載入 " + Banned.Count + " 筆。");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("讀取封鎖名單失敗：" + e.Message);
            }
        }

        internal static bool IsBanned(ulong steamId)
        {
            return steamId != 0UL && Banned.ContainsKey(steamId);
        }

        internal static void Add(ulong steamId, string name)
        {
            if (steamId == 0UL || Banned.ContainsKey(steamId)) return;
            Banned[steamId] = name ?? "";
            Save();
        }

        internal static void Remove(ulong steamId)
        {
            if (!Banned.Remove(steamId)) return;
            Save();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var sb = new StringBuilder(Header());
                foreach (var kv in Banned)
                {
                    sb.Append(kv.Key.ToString(CultureInfo.InvariantCulture));
                    if (!string.IsNullOrEmpty(kv.Value)) sb.Append("  # ").Append(kv.Value);
                    sb.Append('\n');
                }
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("寫入封鎖名單失敗：" + e.Message);
            }
        }

        /// <summary>
        /// 檔頭。這個檔案沒辦法跟著語言重畫（寫進去就是那串字），所以中英併列
        /// ——跟 `.cfg` 的註解、RadioMusic 的說明檔同一條規則。
        /// </summary>
        private static string Header()
        {
            return "# HtF.Guardian —— 封鎖名單。一行一個 Steam ID（17 位數字），# 之後是註解。\n"
                 + "# HtF.Guardian ban list. One Steam ID (17 digits) per line; anything after # is a comment.\n"
                 + "#\n"
                 + "# 這裡的人一連進來就會被踢掉。刪掉那一行即可解除封鎖，\n"
                 + "# 遊戲執行中改檔案的話，要到面板上按「重新載入名單」才會生效。\n"
                 + "# Anyone listed here is kicked as soon as they connect. Delete the line to unban.\n"
                 + "# If you edit this file while the game is running, press \"Reload list\" in the panel.\n";
        }
    }
}

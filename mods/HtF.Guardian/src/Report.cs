using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>擋下來的原因。存成列舉而不是字串，面板才翻得動。</summary>
    internal enum Why
    {
        身分不符,
        不是持有者,
        不是模擬者,
        不是駕駛,
        數值超出範圍,
        移動過快,
        索引超出範圍,
        價格不符,
        冒用身分,
        太頻繁,
        重複生成,
        已封鎖,
        限房主,
        目標無效,
    }

    /// <summary>面板上的一列：一條連線的累計狀態。</summary>
    internal sealed class Offender
    {
        internal int ClientId;
        internal ulong SteamId;
        internal string Name = "";
        internal int Count;
        internal Why LastWhy;
        internal string LastRpc = "";
        internal float LastTime;
        internal bool Punished;
    }

    /// <summary>擋下來的一次事件，給面板下方的清單用。</summary>
    internal struct Incident
    {
        internal float Time;
        internal int ClientId;
        internal string Name;
        internal string Rpc;
        internal Why Why;
    }

    /// <summary>
    /// 守衛的共用出口：記一次違規、決定要不要處置、餵資料給面板。
    ///
    /// **每個公開方法都不能丟例外。** 它們是在 <c>RpcLogic___*</c> 的 prefix 裡跑的，
    /// 而 FishNet 把「RPC 執行期間丟例外」當成惡意封包，會直接踢掉發送者
    /// （<c>ServerManager.cs:1111-1119</c> 的 <c>Kick(KickReason.MalformedData)</c>）。
    /// 一個守衛自己的 bug 不該變成踢人。
    /// </summary>
    internal static class G
    {
        internal const int MaxEvents = 80;

        internal static readonly Dictionary<int, Offender> Offenders = new Dictionary<int, Offender>();
        internal static readonly List<Incident> Events = new List<Incident>();

        /// <summary>總計，面板標題用。</summary>
        internal static int TotalBlocked;

        // 「同一條連線、同一個 RPC 在冷卻時間內只寫一次 log」用的
        private static readonly Dictionary<long, float> LastLogged = new Dictionary<long, float>();

        // ------------------------------------------------------------------ 守衛用的入口

        /// <summary>擋下這次呼叫並記一筆。回傳 false，讓 prefix 直接 <c>return G.Deny(...)</c>。</summary>
        internal static bool Deny(string rpc, Why why)
        {
            Flag(rpc, why);
            return false;
        }

        /// <summary>
        /// 記一筆但**不擋**。用在「把參數改成正確值之後仍然放行」的情況——
        /// 免費旗標、價格不符、冒名發言都屬於這種：擋掉會讓正常的操作看起來壞掉，
        /// 改對再放行才是對的，但仍然要留下紀錄，房主才看得到有人在試。
        /// </summary>
        internal static void Flag(string rpc, Why why)
        {
            try { Record(Sender.Current, rpc, why); }
            catch (Exception) { }
        }

        /// <summary>
        /// 速率桶。超過就當一次違規擋掉。
        /// <paramref name="perSecond"/> 是「這個 RPC 正常玩最多會送多快」，
        /// 各守衛自己給，再統一乘上設定裡的倍率。
        /// </summary>
        internal static bool Rate(string rpc, float perSecond)
        {
            if (Plugin.CheckRates == null || !Plugin.CheckRates.Value) return true;
            if (Limiter.Allow(Sender.Current, rpc, perSecond)) return true;
            return Deny(rpc, Why.太頻繁);
        }

        // ------------------------------------------------------------------ 記錄與處置

        private static void Record(NetworkConnection conn, string rpc, Why why)
        {
            TotalBlocked++;

            int id = ReferenceEquals(conn, null) ? -1 : conn.ClientId;
            float now = Time.unscaledTime;

            Offender o;
            if (!Offenders.TryGetValue(id, out o))
            {
                o = new Offender { ClientId = id, SteamId = Sender.SteamIdOf(conn) };
                Offenders[id] = o;
            }
            o.Count++;
            o.LastWhy = why;
            o.LastRpc = rpc;
            o.LastTime = now;
            if (string.IsNullOrEmpty(o.Name)) o.Name = NameOf(conn);

            Events.Add(new Incident { Time = now, ClientId = id, Name = o.Name, Rpc = rpc, Why = why });
            if (Events.Count > MaxEvents) Events.RemoveRange(0, Events.Count - MaxEvents);

            MaybeLog(id, rpc, why, o, now);
            MaybePunish(conn, o);
        }

        private static void MaybeLog(int id, string rpc, Why why, Offender o, float now)
        {
            // key 是 (clientId, rpc)。rpc 全是程式裡的字面值，字串常數池共用，
            // 所以 GetHashCode 穩定且不配置記憶體。
            long key = ((long)id << 32) ^ (uint)rpc.GetHashCode();
            float last;
            float cd = Plugin.LogCooldown != null ? Plugin.LogCooldown.Value : 2f;
            if (cd > 0f && LastLogged.TryGetValue(key, out last) && now - last < cd) return;
            LastLogged[key] = now;

            Plugin.Log.LogWarning(string.Format("擋下 {0} ← {1} (id {2})：{3}　累計 {4} 次",
                rpc, string.IsNullOrEmpty(o.Name) ? "?" : o.Name, id, why, o.Count));
        }

        private static void MaybePunish(NetworkConnection conn, Offender o)
        {
            if (o.Punished || ReferenceEquals(conn, null)) return;

            int limit = Plugin.ViolationLimit != null ? Plugin.ViolationLimit.Value : 0;
            if (limit <= 0 || o.Count < limit) return;

            OnLimit action = Plugin.Action != null ? Plugin.Action.Value : OnLimit.只記錄;
            if (action == OnLimit.只記錄) return;

            o.Punished = true;

            if (action == OnLimit.踢出並封鎖 && o.SteamId != 0UL)
                Bans.Add(o.SteamId, o.Name);

            Kick(conn, o.Name, action == OnLimit.踢出並封鎖);
        }

        // ------------------------------------------------------------------ 踢人／封鎖

        internal static void Kick(NetworkConnection conn, string name, bool banned)
        {
            if (ReferenceEquals(conn, null)) return;
            try
            {
                conn.Kick(KickReason.UnusualActivity, LoggingType.Warning,
                    "HtF.Guardian: too many rejected ServerRpcs from this connection.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("踢出連線失敗：" + e.Message);
                return;
            }

            string what = banned ? "已封鎖並踢出" : "已踢出";
            Plugin.Log.LogWarning(what + " " + (string.IsNullOrEmpty(name) ? "?" : name)
                                  + "（id " + conn.ClientId + "）");
            Announce(banned ? L.AnnounceBanned(name) : L.AnnounceKicked(name));
        }

        internal static void Announce(string message)
        {
            if (Plugin.AnnounceInChat == null || !Plugin.AnnounceInChat.Value) return;
            try { ChatManager.ChatMessage("[Guardian] " + message); }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ 雜項

        internal static string NameOf(NetworkConnection conn)
        {
            Player p = Sender.Find(conn);
            if (p)
            {
                try { if (!string.IsNullOrEmpty(p.SteamName)) return p.SteamName; }
                catch (Exception) { }
            }
            ulong id = Sender.SteamIdOf(conn);
            return id != 0UL ? id.ToString() : "?";
        }

        internal static void Forget(int clientId)
        {
            Offenders.Remove(clientId);
            Limiter.Forget(clientId);
        }

        internal static void Reset()
        {
            Offenders.Clear();
            Events.Clear();
            LastLogged.Clear();
            Limiter.Clear();
            Speed.Clear();
            TotalBlocked = 0;
        }
    }
}

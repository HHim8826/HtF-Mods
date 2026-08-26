using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;

namespace HtF.Guardian
{
    /// <summary>
    /// 連線層的守衛：封鎖名單在這裡生效，斷線的統計資料也在這裡清掉，
    /// 換島時也在這裡讓價目表失效。
    ///
    /// <c>ServerManager</c> 要等 <c>NetworkManager</c> 起來才存在，所以定期比一次
    /// 目前的實例，換了就重新掛事件。這比在某個「開房完成」的時機點掛一次可靠
    /// ——那種時機點遊戲改版就會變。
    /// </summary>
    internal static class Watcher
    {
        private static ServerManager _attached;
        private static float _nextPoll;
        private static bool _wasHosting;
        private static int _lastIsland = -1;

        /// <summary>
        /// <c>NetworkManager</c>，沒有就 null。
        ///
        /// **不要直接用 <c>InstanceFinder.NetworkManager</c>**：它在找不到的時候
        /// 每次呼叫都會 <c>Debug.Log("NetworkManager not found in any open scenes.")</c>
        /// （FishNet 的 InstanceFinder.cs:40），在主選單每幀問一次就是每幀一行 log。
        /// <c>NetworkManager.Instances</c> 是靜態集合，先看它有沒有東西就不會踩到。
        /// </summary>
        private static NetworkManager Manager()
        {
            try
            {
                return NetworkManager.Instances.Count > 0 ? InstanceFinder.NetworkManager : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>目前是不是房主（伺服器已啟動）。面板用它決定要不要顯示內容。</summary>
        internal static bool IsHosting
        {
            get
            {
                try
                {
                    NetworkManager nm = Manager();
                    return nm != null && nm.IsServerStarted;
                }
                catch (Exception) { return false; }
            }
        }

        internal static void Tick()
        {
            // 每幀都問沒有意義——這些狀態換得很慢。
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.5f;

            NetworkManager nm = Manager();

            Attach(nm);
            WatchHosting(nm);
            WatchIsland();
        }

        /// <summary>掛連線事件。實例換掉才需要重掛。</summary>
        private static void Attach(NetworkManager nm)
        {
            ServerManager next = nm != null ? nm.ServerManager : null;
            if (ReferenceEquals(next, _attached)) return;

            Detach();
            if (next == null) return;

            _attached = next;
            _attached.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        /// <summary>
        /// 開新的一場就把統計清掉。
        ///
        /// **判準是 <c>IsServerStarted</c> 的 false → true 邊緣，不是實例換掉。**
        /// FishNet 的 <c>NetworkManager</c> 預設是 <c>DontDestroyOnLoad</c>
        /// （<c>NetworkManager.cs:807</c>、<c>_dontDestroyOnLoad = true</c>），
        /// <c>ServerManager</c> 掛在同一個物件上，所以它活過場景載入與退房——
        /// 拿實例的參照去比，第一次之後就再也不會變，統計會把好幾場混在一起。
        /// </summary>
        private static void WatchHosting(NetworkManager nm)
        {
            bool hosting = false;
            try { hosting = nm != null && nm.IsServerStarted; }
            catch (Exception) { }

            if (hosting && !_wasHosting)
            {
                G.Reset();
                Prices.Invalidate();
                _lastIsland = -1;
            }
            _wasHosting = hosting;
        }

        /// <summary>
        /// 換島就讓價目表失效。
        ///
        /// <see cref="Prices"/> 的快取有 15 秒節流，而販賣點是**關卡物件**：
        /// 不在換島時清掉的話，那 15 秒內兩個方向都會錯——教學島那個免費魚餌攤
        /// 留下的 <c>0</c> 會讓 <c>cost = 0</c> 在新島上被放行（正好是要擋的洞），
        /// 而新島比較貴的餌又會被當成價格不符、用舊島的價格改寫扣款。
        /// </summary>
        private static void WatchIsland()
        {
            int island;
            try { island = OnlineIslandManager.CurIsland; }
            catch (Exception) { return; }

            if (island == _lastIsland) return;
            _lastIsland = island;
            Prices.Invalidate();
        }

        internal static void Detach()
        {
            _wasHosting = false;
            if (_attached == null) return;
            try { _attached.OnRemoteConnectionState -= OnRemoteConnectionState; }
            catch (Exception) { }
            _attached = null;
        }

        private static void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            try
            {
                if (args.ConnectionState == RemoteConnectionState.Stopped)
                {
                    G.Forget(args.ConnectionId);
                    return;
                }

                if (args.ConnectionState != RemoteConnectionState.Started) return;
                if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;

                ulong steamId = Sender.SteamIdOf(conn);
                if (!Bans.IsBanned(steamId)) return;

                string note;
                Bans.Banned.TryGetValue(steamId, out note);
                Plugin.Log.LogWarning("封鎖名單命中，踢出 " + steamId
                                      + (string.IsNullOrEmpty(note) ? "" : "（" + note + "）"));
                G.Announce(L.AnnounceBannedJoin(string.IsNullOrEmpty(note) ? steamId.ToString() : note));
                conn.Kick(KickReason.UnusualActivity, LoggingType.Warning,
                    "HtF.Guardian: this Steam ID is on the host's ban list.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("處理連線狀態時出錯：" + e.Message);
            }
        }

        /// <summary>面板用：目前連進來的所有連線。</summary>
        internal static List<NetworkConnection> Connections(List<NetworkConnection> into)
        {
            into.Clear();
            try
            {
                NetworkManager nm = Manager();
                ServerManager sm = nm != null ? nm.ServerManager : null;
                if (sm == null) return into;
                foreach (var kv in sm.Clients)
                    if (kv.Value != null) into.Add(kv.Value);
            }
            catch (Exception) { }
            return into;
        }
    }
}

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
    /// 連線層的守衛：封鎖名單在這裡生效，斷線的統計資料也在這裡清掉。
    ///
    /// <c>ServerManager</c> 要等 <c>NetworkManager</c> 起來才存在，而它會隨著
    /// 開房／退房來回出現，所以每幀比一次目前的實例，換了就重新掛事件。
    /// 這比在某個「開房完成」的時機點掛一次可靠——那種時機點遊戲改版就會變。
    /// </summary>
    internal static class Watcher
    {
        private static ServerManager _attached;
        private static float _nextPoll;

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
            // 每幀都問沒有意義——ServerManager 只有開房／退房時會換。
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.5f;

            NetworkManager nm = Manager();
            ServerManager next = nm != null ? nm.ServerManager : null;
            if (ReferenceEquals(next, _attached)) return;

            Detach();
            if (next == null) return;

            _attached = next;
            _attached.OnRemoteConnectionState += OnRemoteConnectionState;
            // 換了一個伺服器實例＝換了一場遊戲，舊的統計沒有意義。
            G.Reset();
        }

        internal static void Detach()
        {
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

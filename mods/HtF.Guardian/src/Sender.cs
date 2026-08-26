using System;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;

namespace HtF.Guardian
{
    /// <summary>
    /// 「現在正在處理的 ServerRpc 是誰送來的」。
    ///
    /// FishNet 把發送端連線交給每個 <c>RpcReader___*</c>（reader 的第三個參數
    /// <c>NetworkConnection conn</c>，由 FishNet 自己填，客戶端偽造不了），
    /// 但遊戲的 reader 只有 <c>SpawnPlayer</c> 那一個把它往下傳。這裡在 reader
    /// 的 prefix 把它接起來，讓所有 <c>RpcLogic___*</c> 的守衛都拿得到。
    ///
    /// **為什麼一個靜態欄位就夠**：reader 讀完參數之後就在同一個呼叫堆疊裡直接
    /// 呼叫 logic，中間不會 await、不會換執行緒，也不會有另一個 reader 插進來
    /// （FishNet 是在主執行緒的 tick 裡循序處理封包的）。prefix 設值、postfix 清掉，
    /// 期間就是那一個 RPC 的處理過程。
    ///
    /// 如果 logic 執行到一半丟例外，postfix 不會跑，欄位會留著上一個值——
    /// 但那條連線接著就會被 FishNet 以 <c>MalformedData</c> 踢掉
    /// （見 `AI_CONTEXT.md` 第 3 節），而下一個 reader 的 prefix 會覆蓋掉它，
    /// 所以不會有殘值被誤用。
    /// </summary>
    internal static class Sender
    {
        /// <summary>發送端連線。null = 不是從網路進來的（伺服器自己呼叫）。</summary>
        internal static NetworkConnection Current;

        private static Player _player;
        private static bool _playerResolved;

        internal static void Begin(NetworkConnection conn)
        {
            Current = conn;
            _player = null;
            _playerResolved = false;
        }

        internal static void End()
        {
            Current = null;
            _player = null;
            _playerResolved = false;
        }

        /// <summary>
        /// 房主自己（或伺服器內部直接呼叫）。遊戲有好幾處是在伺服器端代別人送 RPC 的
        /// ——爆炸傷害（<c>ExplosionManager.ServerExplode</c>）、Boss 攻擊、
        /// 引信到期的炸藥——那些的發送端都是房主，用一般規則檢查一定誤判。
        /// </summary>
        internal static bool IsHost
        {
            get
            {
                NetworkConnection c = Current;
                if (c == null) return true;   // 不是從網路進來的＝伺服器自己

                // 三個獨立訊號，任一成立就算房主。**刻意不只靠 IsLocalClient**：
                // 它是 `NetworkManager != null && NetworkManager.ClientManager.Connection == this`，
                // 而 NetworkConnection.NetworkManager 是可能沒被設起來的
                // （FishNet 自己在 GetAddress 那條路上就有 `if (NetworkManager == null)
                // NetworkManager = InstanceFinder.NetworkManager;` 這種補救）。
                // 那個欄位一旦是 null，IsLocalClient 就靜靜地回 false，
                // 房主的每一個封包都會被當成外人檢查——實測踩到過。
                if (Try(() => c.IsLocalClient)) return true;

                // 備援一：直接跟本機客戶端的連線比 ClientId，不經過 conn.NetworkManager。
                if (Try(() =>
                {
                    NetworkManager nm = NetworkManager.Instances.Count > 0 ? InstanceFinder.NetworkManager : null;
                    return nm != null && Same(c, nm.ClientManager.Connection);
                })) return true;

                // 備援二：完全不碰 FishNet 的連線語意——房主操作的就是 Player.LocalPlayer。
                if (Try(() =>
                {
                    Player local = Player.LocalPlayer;
                    return local && Same(local.Owner, c);
                })) return true;

                Diagnose(c);
                return false;
            }
        }

        private static bool Try(Func<bool> f)
        {
            try { return f(); }
            catch (Exception) { return false; }
        }

        private static bool _diagnosed;

        /// <summary>
        /// 第一次判定「這條連線不是房主」時，把判斷依據印出來。
        /// 房主豁免失效是這個 mod 最難察覺的故障——所有守衛都還在動，
        /// 只是全部套在自己身上，看起來就像「正常玩也會被擋」。
        /// </summary>
        private static void Diagnose(NetworkConnection c)
        {
            if (_diagnosed) return;
            _diagnosed = true;
            try
            {
                NetworkManager nm = NetworkManager.Instances.Count > 0 ? InstanceFinder.NetworkManager : null;
                NetworkConnection local = nm != null ? nm.ClientManager.Connection : null;
                Player lp = Player.LocalPlayer;
                Plugin.Log.LogInfo(string.Format(
                    "第一條被視為外部連線的封包：conn id {0}、本機客戶端 id {1}、LocalPlayer owner id {2}。"
                    + "如果這三個是同一個數字，代表房主豁免判斷有問題，請回報。",
                    c.ClientId,
                    ReferenceEquals(local, null) ? -1 : local.ClientId,
                    (lp && !ReferenceEquals(lp.Owner, null)) ? lp.Owner.ClientId : -1));
            }
            catch (Exception) { }
        }

        /// <summary>這個 RPC 要不要驗證。房主預設跳過，見「也檢查房主自己」設定。</summary>
        internal static bool Exempt
        {
            get
            {
                if (Plugin.Enabled == null || !Plugin.Enabled.Value) return true;
                return IsHost && !Plugin.AlsoCheckHost.Value;
            }
        }

        /// <summary>發送端的 Player。第一次查之後在這個 RPC 的處理過程中快取。</summary>
        internal static Player Player
        {
            get
            {
                if (_playerResolved) return _player;
                _playerResolved = true;
                _player = Find(Current);
                return _player;
            }
        }

        /// <summary>這個 Player 是不是發送端本人。null 一律回 false。</summary>
        internal static bool Owns(Player p)
        {
            if (!p) return false;
            try { return Same(p.Owner, Current); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// 兩條連線是不是同一條。刻意不用 <c>NetworkConnection</c> 的 <c>==</c>：
        /// 它的 <c>Equals</c> 在 <c>ClientId == -1</c>（未初始化的連線）時回 false，
        /// 而我們要的是「同一個 ClientId」這個明確的意思。
        /// </summary>
        internal static bool Same(NetworkConnection a, NetworkConnection b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
            return a.ClientId >= 0 && a.ClientId == b.ClientId;
        }

        internal static Player Find(NetworkConnection conn)
        {
            if (ReferenceEquals(conn, null)) return null;
            try
            {
                var players = PlayerManager.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    Player p = players[i];
                    if (p && Same(p.Owner, conn)) return p;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// 發送端的 Steam ID，拿不到就回 0。
        ///
        /// **來源是 <c>conn.GetAddress()</c>，而「它就是 SteamID」這件事不是
        /// 從 API 名稱推論的——是遊戲自己這樣用的。** <c>RpcLogic___SpawnPlayer</c>
        /// 在 <c>ConnectionManager.IsUsingSteam</c> 為真時做的正是
        /// <c>ulong.TryParse(conn.GetAddress(), out steamID)</c> 再拿去
        /// <c>SteamManager.IsCurrentLobbyMember</c> 比對大廳成員；沒過就踢人。
        /// 也就是說在 Steam 傳輸底下，這個值已經被遊戲當成身分在用了。
        ///
        /// 不是 Steam 傳輸（或位址不是那個格式）時這裡回 0，而所有吃身分的功能
        /// ——冒名發言改寫、封鎖名單、面板上的 Steam ID——都會跟著失效。
        /// 那種情況**不能默默過去**，所以第一次遇到會在 log 留一行。
        /// </summary>
        internal static ulong SteamIdOf(NetworkConnection conn)
        {
            if (ReferenceEquals(conn, null)) return 0UL;
            try
            {
                ulong id;
                if (ulong.TryParse(conn.GetAddress(), out id) && id != 0UL) return id;
                WarnNoSteamIdentity(conn);
                return 0UL;
            }
            catch (Exception) { return 0UL; }
        }

        private static bool _warnedNoIdentity;

        private static void WarnNoSteamIdentity(NetworkConnection conn)
        {
            if (_warnedNoIdentity) return;
            // 本機連線在傳輸層上本來就沒有 Steam 位址，那不是異常。
            try { if (conn.IsLocalClient) return; } catch (Exception) { }

            _warnedNoIdentity = true;
            Plugin.Log.LogWarning(
                "連線的位址不是 Steam ID（可能不是 Steam 傳輸）。"
                + "封鎖名單、冒名發言改寫、面板上的 Steam ID 這一場都不會生效，"
                + "其餘守衛照常運作。");
        }

        internal static ulong SteamId { get { return SteamIdOf(Current); } }
    }
}

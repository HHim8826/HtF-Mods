using System;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet.Connection;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 房主的監控面板。連線清單、擋下來的事件、封鎖名單，各配一顆按鈕。
    ///
    /// **全部用固定 Rect 的 <c>GUI.*</c> 畫，沒有用 GUILayout。**
    /// `MODDING_CONTEXT.md` 第 4 節那條「會改變版面結構的狀態變更要延到 Layout 事件」
    /// 是 GUILayout 專屬的問題——它在 Layout 幀算好控件樹，之後的事件照那棵樹取值，
    /// 對不上就 NRE。固定 Rect 沒有那棵樹，所以切分頁、加一列事件、按鈕變成「確定？」
    /// 都可以當場生效，不必 Defer。代價是要自己算座標。
    /// </summary>
    internal static class Panel
    {
        internal static bool Visible;

        private const int WindowId = 0x47554152;   // "GUAR"
        private const float W = 760f, H = 480f;
        private const float Row = 26f;

        private enum Tab { 連線, 事件, 封鎖 }
        private static Tab _tab = Tab.連線;

        private static Rect _rect = new Rect(-1f, -1f, W, H);
        private static Vector2 _scroll;

        // 兩段式確認：第一下把按鈕變成「確定？」，第二下才真的做。
        // 存的是「哪一顆按鈕」加「什麼時候按的」，超過幾秒自動解除。
        private static string _armed;
        private static float _armedAt;
        private const float ArmSeconds = 3f;

        /// <summary>畫面上的一列連線。<see cref="Refresh"/> 算好，<c>Draw</c> 只讀。</summary>
        private struct ConnRow
        {
            internal NetworkConnection Conn;
            internal int ClientId;
            internal string Name;
            internal ulong Steam;
            internal bool IsHost;
            internal int Count;
            internal string Last;
        }

        private static readonly List<NetworkConnection> Conns = new List<NetworkConnection>();
        private static readonly List<ConnRow> Rows = new List<ConnRow>();
        private static float _nextRefresh;

        private static Font _font;
        private static bool _fontTried;
        private static Styles _s;

        // ------------------------------------------------------------------ 生命週期

        internal static void Toggle()
        {
            Visible = !Visible;
            _armed = null;
            _nextRefresh = 0f;
            SetMouse(Visible);
        }

        internal static void Tick()
        {
            if (!Visible) return;
            // 遊戲有幾處會把游標搶回去（OnApplicationFocus、暫停），每幀壓住。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_armed != null && Time.unscaledTime - _armedAt > ArmSeconds) _armed = null;
            Refresh();
        }

        /// <summary>
        /// 連線清單的快照。**在 Update 裡算，不在 OnGUI 裡算**，兩個理由：
        /// OnGUI 一幀會跑好幾次（Layout、輸入、Repaint），在裡面算等於做好幾遍；
        /// 而且 <c>NetworkConnection.GetAddress()</c> 會問到傳輸層（Steam），
        /// 那不是可以一幀呼叫十幾次的東西。順帶讓捲動區的內容高度在一幀之內固定。
        /// </summary>
        private static void Refresh()
        {
            float now = Time.unscaledTime;
            if (now < _nextRefresh) return;
            _nextRefresh = now + 0.5f;

            Rows.Clear();
            if (!Watcher.IsHosting) return;

            Watcher.Connections(Conns);
            for (int i = 0; i < Conns.Count; i++)
            {
                NetworkConnection c = Conns[i];
                Offender o;
                G.Offenders.TryGetValue(c.ClientId, out o);

                bool isHost = false;
                try { isHost = c.IsLocalClient; } catch (Exception) { }

                Rows.Add(new ConnRow
                {
                    Conn = c,
                    ClientId = c.ClientId,
                    Name = o != null && !string.IsNullOrEmpty(o.Name) ? o.Name : G.NameOf(c),
                    Steam = Sender.SteamIdOf(c),
                    IsHost = isHost,
                    Count = o != null ? o.Count : 0,
                    Last = o == null ? "—" : L.WhyText(o.LastWhy) + " · " + o.LastRpc,
                });
            }
        }

        private static void SetMouse(bool unlock)
        {
            // 關閉時不要自己把游標鎖回去：PlayerCamera.ToggleMouse 內部已經處理了
            // 「暫停中 / 在主選單 / 思考中」這幾種必須保持解鎖的狀況。
            try { PlayerCamera.ToggleMouse(unlock); }
            catch (Exception) { }
            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // ------------------------------------------------------------------ 繪製

        internal static void Draw()
        {
            if (!Visible) return;

            GUI.depth = -1000;   // 壓在其他 IMGUI 之上
            EnsureFont();
            Styles.Ensure(ref _s, _font);

            float scale = Plugin.UiScale != null ? Plugin.UiScale.Value : 1f;
            if (_rect.x < 0f)
            {
                _rect.x = Mathf.Max(10f, (Screen.width / scale - W) * 0.5f);
                _rect.y = Mathf.Max(10f, (Screen.height / scale - H) * 0.4f);
            }

            GUISkin prevSkin = GUI.skin;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.skin = _s.Skin;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            try { _rect = GUI.Window(WindowId, _rect, DrawWindow, L.Title); }
            catch (Exception e) { Plugin.Log.LogWarning("面板繪製失敗：" + e.Message); }

            GUI.matrix = prevMatrix;
            GUI.skin = prevSkin;
        }

        private static void DrawWindow(int id)
        {
            float y = 30f;

            // --- 狀態列 ---
            GUI.Label(new Rect(14f, y, W - 28f, 18f), StatusLine(), _s.Dim);
            y += 24f;

            // --- 分頁 ---
            DrawTab(new Rect(14f, y, 110f, 24f), Tab.連線, L.TabPlayers);
            DrawTab(new Rect(128f, y, 110f, 24f), Tab.事件, L.TabEvents);
            DrawTab(new Rect(242f, y, 110f, 24f), Tab.封鎖, L.TabBans);
            y += 32f;

            GUI.Box(new Rect(14f, y, W - 28f, 1f), GUIContent.none, _s.Line);
            y += 8f;

            Rect body = new Rect(14f, y, W - 28f, H - y - 46f);

            if (Plugin.Enabled != null && !Plugin.Enabled.Value)
                GUI.Label(new Rect(body.x, body.y + 8f, body.width, 40f), L.Disabled, _s.Warn);
            else if (!Watcher.IsHosting)
                GUI.Label(new Rect(body.x, body.y + 8f, body.width, 46f), L.NotHosting, _s.Warn);
            else
                switch (_tab)
                {
                    case Tab.事件: DrawEvents(body); break;
                    case Tab.封鎖: DrawBans(body); break;
                    default: DrawConnections(body); break;
                }

            DrawFooter();
            GUI.DragWindow(new Rect(0f, 0f, W, 26f));
        }

        private static string StatusLine()
        {
            string s = L.Coverage(Patcher.Guarded.Count, Patcher.ServerRpcTotal, Patcher.ReadersPatched)
                     + "　·　" + L.Blocked(G.TotalBlocked);
            if (Patcher.Missing.Count > 0) s += "　·　" + L.MissingTargets(Patcher.Missing.Count);
            return s;
        }

        private static void DrawTab(Rect r, Tab tab, string label)
        {
            bool on = _tab == tab;
            if (GUI.Button(r, label, on ? _s.TabOn : _s.Tab) && !on)
            {
                _tab = tab;
                _scroll = Vector2.zero;
                _armed = null;
            }
        }

        // ------------------------------------------------------------------ 連線

        private static void DrawConnections(Rect body)
        {
            if (Rows.Count == 0)
            {
                GUI.Label(new Rect(body.x, body.y + 6f, body.width, 20f), L.NoPlayers, _s.Dim);
                return;
            }

            float[] cols = { 0f, 250f, 330f, 430f, 570f };
            GUI.Label(new Rect(body.x + cols[0], body.y, 240f, 18f), L.ColPlayer, _s.Head);
            GUI.Label(new Rect(body.x + cols[1], body.y, 70f, 18f), L.ColViolations, _s.Head);
            GUI.Label(new Rect(body.x + cols[2], body.y, 90f, 18f), L.ColSteamId, _s.Head);
            GUI.Label(new Rect(body.x + cols[3], body.y, 130f, 18f), L.ColLast, _s.Head);
            GUI.Label(new Rect(body.x + cols[4], body.y, 150f, 18f), L.ColAction, _s.Head);

            Rect view = new Rect(body.x, body.y + 22f, body.width, body.height - 22f);
            Rect content = new Rect(0f, 0f, view.width - 18f, Rows.Count * Row);
            _scroll = GUI.BeginScrollView(view, _scroll, content);

            for (int i = 0; i < Rows.Count; i++)
            {
                ConnRow r = Rows[i];
                float ry = i * Row;
                if (i % 2 == 1) GUI.Box(new Rect(0f, ry, content.width, Row), GUIContent.none, _s.Stripe);

                GUI.Label(new Rect(cols[0], ry + 4f, 240f, 18f),
                    r.Name + (r.IsHost ? " " + L.Host : ""), _s.Text);

                GUI.Label(new Rect(cols[1], ry + 4f, 70f, 18f), r.Count.ToString(),
                    r.Count == 0 ? _s.Dim : (r.Count >= Threshold() ? _s.Bad : _s.Warn));

                GUI.Label(new Rect(cols[2], ry + 4f, 100f, 18f),
                    r.Steam == 0UL ? "—" : r.Steam.ToString(), _s.Dim);

                GUI.Label(new Rect(cols[3], ry + 4f, 135f, 18f), r.Last, _s.Dim);

                if (r.IsHost) continue;

                if (Confirm(new Rect(cols[4], ry + 3f, 66f, 20f), "kick" + r.ClientId, L.Kick))
                    G.Kick(r.Conn, r.Name, false);

                if (Confirm(new Rect(cols[4] + 72f, ry + 3f, 66f, 20f), "ban" + r.ClientId, L.Ban))
                {
                    Bans.Add(r.Steam, r.Name);
                    G.Kick(r.Conn, r.Name, true);
                }
            }

            GUI.EndScrollView();
        }

        private static int Threshold()
        {
            int limit = Plugin.ViolationLimit != null ? Plugin.ViolationLimit.Value : 0;
            return limit > 0 ? limit : int.MaxValue;
        }

        // ------------------------------------------------------------------ 事件

        private static void DrawEvents(Rect body)
        {
            if (G.Events.Count == 0)
            {
                GUI.Label(new Rect(body.x, body.y + 6f, body.width, 20f), L.NoEvents, _s.Dim);
                return;
            }

            Rect view = new Rect(body.x, body.y, body.width, body.height);
            Rect content = new Rect(0f, 0f, view.width - 18f, G.Events.Count * 20f);
            _scroll = GUI.BeginScrollView(view, _scroll, content);

            float now = Time.unscaledTime;
            // 新的在上面：事件是往後追加的，所以反著走。
            for (int i = 0; i < G.Events.Count; i++)
            {
                Incident e = G.Events[G.Events.Count - 1 - i];
                float ry = i * 20f;
                if (i % 2 == 1) GUI.Box(new Rect(0f, ry, content.width, 20f), GUIContent.none, _s.Stripe);

                GUI.Label(new Rect(2f, ry + 2f, 78f, 16f), L.SecondsAgo(now - e.Time), _s.Dim);
                GUI.Label(new Rect(84f, ry + 2f, 170f, 16f), e.Name ?? "?", _s.Text);
                GUI.Label(new Rect(258f, ry + 2f, 250f, 16f), e.Rpc, _s.Text);
                GUI.Label(new Rect(512f, ry + 2f, 200f, 16f), L.WhyText(e.Why), _s.Warn);
            }

            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ 封鎖名單

        private static readonly List<ulong> BanKeys = new List<ulong>();

        private static void DrawBans(Rect body)
        {
            BanKeys.Clear();
            foreach (var kv in Bans.Banned) BanKeys.Add(kv.Key);

            if (BanKeys.Count == 0)
            {
                GUI.Label(new Rect(body.x, body.y + 6f, body.width, 20f), L.NoBans, _s.Dim);
                return;
            }

            Rect view = new Rect(body.x, body.y, body.width, body.height);
            Rect content = new Rect(0f, 0f, view.width - 18f, BanKeys.Count * Row);
            _scroll = GUI.BeginScrollView(view, _scroll, content);

            for (int i = 0; i < BanKeys.Count; i++)
            {
                ulong id = BanKeys[i];
                float ry = i * Row;
                if (i % 2 == 1) GUI.Box(new Rect(0f, ry, content.width, Row), GUIContent.none, _s.Stripe);

                string note;
                Bans.Banned.TryGetValue(id, out note);
                GUI.Label(new Rect(2f, ry + 4f, 180f, 18f), id.ToString(), _s.Text);
                GUI.Label(new Rect(190f, ry + 4f, 380f, 18f), note ?? "", _s.Dim);

                if (Confirm(new Rect(584f, ry + 3f, 80f, 20f), "unban" + id, L.Unban))
                    Bans.Remove(id);
            }

            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ 頁尾

        private static void DrawFooter()
        {
            float y = H - 34f;
            if (GUI.Button(new Rect(14f, y, 110f, 24f), L.ResetStats, _s.Button)) G.Reset();
            if (GUI.Button(new Rect(130f, y, 130f, 24f), L.ReloadBans, _s.Button))
            {
                Bans.Load();
                Prices.Invalidate();
            }
            if (GUI.Button(new Rect(266f, y, 120f, 24f), L.OpenFolder, _s.Button)) OpenBanFolder();
            if (GUI.Button(new Rect(W - 94f, y, 80f, 24f), L.Close, _s.Button)) Toggle();
        }

        private static void OpenBanFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Bans.Folder);
                Process.Start(new ProcessStartInfo(Bans.Folder) { UseShellExecute = true });
            }
            catch (Exception e) { Plugin.Log.LogWarning("開啟資料夾失敗：" + e.Message); }
        }

        /// <summary>
        /// 兩段式按鈕：第一下變成「確定？」，第二下才回 true。
        /// 踢人和封鎖都是不可逆的，一次誤點不該就送出去。
        /// </summary>
        private static bool Confirm(Rect r, string key, string label)
        {
            bool armed = _armed == key;
            if (!GUI.Button(r, armed ? Sure() : label, armed ? _s.Danger : _s.Button)) return false;

            if (armed) { _armed = null; return true; }
            _armed = key;
            _armedAt = Time.unscaledTime;
            return false;
        }

        private static string Sure() { return HtF.Shared.Loc.P("確定？", "Sure?"); }

        // ------------------------------------------------------------------ 樣式

        private static void EnsureFont()
        {
            if (_fontTried) return;
            _fontTried = true;
            string name = Plugin.FontName.Value;
            if (string.IsNullOrEmpty(name)) return;
            try { _font = Font.CreateDynamicFontFromOSFont(name, 13); }
            catch (Exception e) { Plugin.Log.LogWarning("載入字型 " + name + " 失敗：" + e.Message); }
        }
    }
}

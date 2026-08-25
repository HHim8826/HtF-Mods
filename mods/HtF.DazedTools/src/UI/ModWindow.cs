using System;
using System.Collections.Generic;
using HtF.DazedTools.Commands;
using HtF.Shared;
using UnityEngine;

namespace HtF.DazedTools.UI
{
    /// <summary>
    /// IMGUI 視窗。
    ///
    /// 重要規則：Unity 每幀會對 OnGUI 跑多次——先 Layout 算版面，再跑滑鼠／鍵盤事件與
    /// Repaint。如果在後面那幾次裡改動了「版面結構」（控件數量、群組巢狀、要不要顯示某塊），
    /// 就會和 Layout 那次算出來的結構對不上，GUILayout 取到 null 直接 NRE。
    ///
    /// 所以這裡的規則是：**任何會改變結構的狀態變更一律 Defer()，只在 Layout 事件套用。**
    /// 純粹改值（文字框內容、開關狀態）不影響結構的才可以直接改。
    /// </summary>
    internal static class ModWindow
    {
        private const int WindowId = 0x0DA2ED;

        internal static bool Visible;
        internal static bool DangerUnlocked;

        private static Rect _rect = new Rect(70f, 50f, 940f, 680f);
        private static int _category;
        private static Vector2 _scroll;
        private static Vector2 _logScroll;
        private static Font _font;
        private static bool _fontTried;

        // 延後到下一個 Layout 事件才執行的狀態變更
        private static readonly List<Action> Pending = new List<Action>();

        // 每個指令的參數暫存值，key = 指令名
        private static readonly Dictionary<string, string[]> Values = new Dictionary<string, string[]>();

        // PosOrVoid 的模式：0 = 我的位置、1 = 座標、2 = 虛空。
        // 不從參數字串反推，否則使用者把座標刪空時結構會在同一幀內變化。
        private static readonly Dictionary<string, int> PosModes = new Dictionary<string, int>();

        // 輸出區。畫的是 Layout 時取的快照，避免遊戲在幀中途寫入訊息改變結構。
        private static readonly List<string> Log = new List<string>();
        private static string[] _logView = new string[0];
        private const int MaxLog = 200;

        // 主控台
        private static string _console = "";
        private static readonly List<string> History = new List<string>();
        private static int _historyIdx = -1;
        private const string ConsoleControl = "dazed_console";

        // 選單覆蓋層
        private static Cmd _pickerCmd;
        private static int _pickerArg = -1;
        private static string[] _pickerLabels, _pickerValues;
        private static string _pickerSearch = "";
        private static Vector2 _pickerScroll;
        // 篩選結果同樣只在 Layout 重算，其餘事件沿用
        private static readonly List<int> PickerView = new List<int>();

        // ------------------------------------------------------------------ 生命週期

        internal static void Toggle()
        {
            Visible = !Visible;
            ClosePicker();
            SetMouse(Visible);
        }

        internal static void Tick()
        {
            if (!Visible) return;
            // 遊戲有幾處會把游標搶回去（OnApplicationFocus、暫停），每幀壓住。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void SetMouse(bool unlock)
        {
            // 關閉時不要自己把游標鎖回去。PlayerCamera.ToggleMouse 內部已經處理了
            // 「暫停中 / 在主選單 / 思考中」這幾種必須保持解鎖的狀況，
            // 我們再硬鎖一次的話，從 ESC 選單裡開啟再關掉，游標就消失了。
            try { PlayerCamera.ToggleMouse(unlock); }
            catch (Exception) { /* 主選單時 GameInfo.Input 可能還沒好 */ }
            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        internal static void PushLog(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Log.Add(message);
            if (Log.Count > MaxLog) Log.RemoveRange(0, Log.Count - MaxLog);
            _logScroll.y = float.MaxValue;
        }

        /// <summary>把會改變版面結構的動作排到下一個 Layout 事件。</summary>
        private static void Defer(Action action)
        {
            Pending.Add(action);
        }

        // ------------------------------------------------------------------ 繪製

        internal static void Draw()
        {
            if (!Visible) return;

            GUI.depth = -1000; // 壓在其他 IMGUI 之上
            EnsureFont();
            Theme.Build(_font);

            if (Event.current.type == EventType.Layout) ApplyPending();

            GUISkin prevSkin = GUI.skin;
            Matrix4x4 prevMatrix = GUI.matrix;

            GUI.skin = Theme.Skin;
            float scale = Plugin.UiScale != null ? Plugin.UiScale.Value : 1f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            _rect = GUILayout.Window(WindowId, _rect, DrawWindow, "DAZED TOOLS");

            GUI.matrix = prevMatrix;
            GUI.skin = prevSkin;
        }

        /// <summary>只在 Layout 事件呼叫：套用延後的變更，然後重算所有派生的結構快照。</summary>
        private static void ApplyPending()
        {
            if (Pending.Count > 0)
            {
                var todo = Pending.ToArray();
                Pending.Clear();
                for (int i = 0; i < todo.Length; i++)
                {
                    try { todo[i](); }
                    catch (Exception e) { Plugin.Log.LogError("延後動作失敗：" + e); }
                }
            }

            _logView = Log.ToArray();
            RebuildPickerView();
        }

        private static void RebuildPickerView()
        {
            PickerView.Clear();
            if (_pickerLabels == null) return;

            string needle = (_pickerSearch ?? "").Trim();
            for (int i = 0; i < _pickerLabels.Length; i++)
            {
                if (needle.Length > 0 &&
                    _pickerLabels[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
                    _pickerValues[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                PickerView.Add(i);
            }
        }

        private static void EnsureFont()
        {
            if (_fontTried) return;
            _fontTried = true;
            string name = Plugin.FontName != null ? Plugin.FontName.Value : "";
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(name, 13);
                if (_font == null) Plugin.Log.LogWarning("找不到字型 " + name + "，改用內建字型。");
            }
            catch (Exception e) { Plugin.Log.LogWarning("載入字型 " + name + " 失敗：" + e.Message); }
        }

        private static void DrawWindow(int id)
        {
            if (_pickerArg >= 0 && _pickerCmd != null)
            {
                DrawPicker();
                GUI.DragWindow(new Rect(0, 0, 100000, 26));
                return;
            }

            DrawHeader();
            Theme.Separator();

            GUILayout.BeginHorizontal();
            DrawCategories();
            GUILayout.Space(10f);
            DrawCommands();
            GUILayout.EndHorizontal();

            Theme.Separator();
            DrawFooter();
            GUI.DragWindow(new Rect(0, 0, 100000, 26));
        }

        private static void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(StatusLine(), Theme.DimLabel);
            GUILayout.FlexibleSpace();

            Color prev = GUI.contentColor;
            GUI.contentColor = DangerUnlocked ? Theme.Danger : Theme.Dim;
            bool unlocked = GUILayout.Toggle(DangerUnlocked, DangerUnlocked ? L.DangerUnlocked : L.DangerLocked,
                                             Theme.Pill, GUILayout.Width(150f));
            GUI.contentColor = prev;
            if (unlocked != DangerUnlocked)
            {
                DangerUnlocked = unlocked;
                Defer(() => PushLog(unlocked ? L.DangerUnlockedLog : L.DangerLockedLog));
            }

            GUILayout.Space(6f);
            if (GUILayout.Button(L.Close, GUILayout.Width(66f))) Defer(Toggle);

            GUILayout.EndHorizontal();
        }

        private static string StatusLine()
        {
            try
            {
                string conn = Server.Instance ? L.Connected : L.Disconnected;
                int players = PlayerManager.Players != null ? PlayerManager.Players.Count : 0;
                string me = Player.LocalPlayer ? Player.LocalPlayer.SteamName : L.NoLocalPlayer;
                return L.StatusLine(conn, players, me);
            }
            catch (Exception) { return L.StatusUnavailable; }
        }

        private static void DrawCategories()
        {
            GUILayout.BeginVertical(GUILayout.Width(118f));
            GUILayout.Space(2f);
            for (int i = 0; i < Registry.Categories.Length; i++)
            {
                bool on = _category == i;
                if (GUILayout.Toggle(on, Registry.CategoryLabel(Registry.Categories[i]), Theme.Tab, GUILayout.Height(28f)) && !on)
                {
                    int pick = i; // 換分頁會換掉整份指令清單，必須延後
                    Defer(() => { _category = pick; _scroll = Vector2.zero; });
                }
            }
            GUILayout.EndVertical();
        }

        private static void DrawCommands()
        {
            float height = Mathf.Max(220f, _rect.height - 258f);

            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height));

            List<Cmd> cmds = Registry.InCategory(Registry.Categories[_category]);
            for (int i = 0; i < cmds.Count; i++) DrawCommandCard(cmds[i]);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawCommandCard(Cmd cmd)
        {
            Color risk = Theme.RiskColor(cmd.Risk);

            GUILayout.BeginHorizontal(Theme.CardBox);
            Theme.VStripe(risk);
            GUILayout.Space(10f);
            GUILayout.BeginVertical();

            // 標題行
            GUILayout.BeginHorizontal();
            Color prev = GUI.contentColor;
            GUI.contentColor = Theme.Accent;
            GUILayout.Label("/" + cmd.Name, Theme.Head, GUILayout.Width(170f));
            GUI.contentColor = risk;
            GUILayout.Label(Theme.RiskLabel(cmd.Risk), Theme.Badge);
            GUI.contentColor = prev;
            GUILayout.Space(6f);
            GUILayout.Label(cmd.SummaryText, Theme.DimLabel);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(cmd.Warning))
                GUILayout.Label("⚠  " + cmd.WarningText, Theme.WarnLabel);

            // 參數行。
            // 參數多的指令（例如 /hitplayer：玩家下拉 + 傷害 + 繞過PvP）加起來會超過
            // 卡片寬度，GUILayout 會把排在最後的東西擠出可視範圍——執行按鈕就這樣消失了。
            // 所以參數自己按寬度預算換行，按鈕永遠獨立一列，不跟參數搶空間。
            string[] vals = ValuesFor(cmd);
            if (cmd.Args.Length > 0)
            {
                GUILayout.Space(4f);
                float budget = Mathf.Max(240f, _rect.width - 118f - 90f);
                float used = 0f;
                GUILayout.BeginHorizontal();
                for (int i = 0; i < cmd.Args.Length; i++)
                {
                    float w = EstimateArgWidth(cmd.Args[i]);
                    if (used > 0f && used + w > budget)
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(3f);
                        GUILayout.BeginHorizontal();
                        used = 0f;
                    }
                    DrawArg(cmd, i, vals);
                    used += w;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool locked = cmd.Risk == Risk.Danger && !DangerUnlocked;
            GUI.enabled = !locked;
            if (GUILayout.Button(locked ? L.Locked : L.Run, locked ? GUI.skin.button : Theme.Run,
                                 GUILayout.Width(76f), GUILayout.Height(24f)))
            {
                string line = Build(cmd, vals); // 現在就組好，延後的只有送出
                Defer(() => Run(line));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 參數標籤的欄寬。中文一個字大約 13px，英文一個字母大約 7.5px——
        /// 兩邊共用同一個係數的話，英文標籤會被切掉一截。
        /// </summary>
        private static float ArgLabelWidth(Arg a)
        {
            float per = Loc.IsEnglish ? 7.5f : 13f;
            return Mathf.Min(112f, per * a.Text.Length + 16f);
        }

        /// <summary>
        /// 估算一個參數控件會佔多寬，用來決定換行。
        /// 數字必須和 DrawArg 裡實際用的寬度一致，否則換行點會抓錯。
        /// </summary>
        private static float EstimateArgWidth(Arg a)
        {
            float label = ArgLabelWidth(a) + 4f;
            float control;
            switch (a.Kind)
            {
                case ArgKind.Bool: control = 52f; break;
                case ArgKind.Flag: control = 40f; break;
                case ArgKind.Vec3: control = 56f * 3f + (string.IsNullOrEmpty(a.Hint) ? 0f : 124f); break;
                case ArgKind.PosOrVoid: control = 74f + 48f + 48f + 4f + 52f * 3f; break;
                case ArgKind.Text: control = 190f + (string.IsNullOrEmpty(a.Hint) ? 0f : 154f); break;
                case ArgKind.Int:
                case ArgKind.Float: control = 84f + (string.IsNullOrEmpty(a.Hint) ? 0f : 154f); break;
                default: control = 226f; break;
            }
            return label + control + 10f;
        }

        private static void DrawArg(Cmd cmd, int index, string[] vals)
        {
            Arg a = cmd.Args[index];
            GUILayout.Label(a.Text, Theme.ArgLabel, GUILayout.Width(ArgLabelWidth(a)));
            GUILayout.Space(4f);

            switch (a.Kind)
            {
                case ArgKind.Bool:
                {
                    bool on = vals[index] == "true";
                    bool now = GUILayout.Toggle(on, on ? L.On : L.Off, Theme.Pill, GUILayout.Width(52f));
                    if (now != on) vals[index] = now ? "true" : "false";
                    break;
                }
                case ArgKind.Flag:
                {
                    bool on = vals[index] == a.Token;
                    bool now = GUILayout.Toggle(on, on ? "✓" : "－", Theme.Pill, GUILayout.Width(40f));
                    if (now != on) vals[index] = now ? a.Token : "";
                    break;
                }
                case ArgKind.Vec3:
                    DrawVec3(vals, index, a);
                    break;

                case ArgKind.PosOrVoid:
                    DrawPosOrVoid(cmd, index, vals);
                    break;

                case ArgKind.Text:
                case ArgKind.Int:
                case ArgKind.Float:
                {
                    float w = a.Kind == ArgKind.Text ? 190f : 84f;
                    vals[index] = GUILayout.TextField(vals[index] ?? "", GUILayout.Width(w));
                    // 提示一律畫出來（即使是空字串），這樣結構不會隨輸入內容改變
                    if (!string.IsNullOrEmpty(a.Hint))
                    {
                        GUILayout.Space(4f);
                        GUILayout.Label(string.IsNullOrEmpty(vals[index]) ? a.HintText : "",
                                        Theme.DimLabel, GUILayout.Width(150f));
                    }
                    break;
                }
                default:
                {
                    if (GUILayout.Button(LabelFor(a, vals[index]) + "   ▾", GUILayout.Width(226f)))
                    {
                        Cmd c = cmd; int arg = index;
                        Defer(() => OpenPicker(c, arg));
                    }
                    break;
                }
            }
            GUILayout.Space(10f);
        }

        private static void DrawVec3(string[] vals, int index, Arg a)
        {
            string[] xyz = SplitVec(vals[index]);
            for (int k = 0; k < 3; k++) xyz[k] = GUILayout.TextField(xyz[k], GUILayout.Width(56f));
            string joined = (xyz[0] + " " + xyz[1] + " " + xyz[2]).Trim();
            vals[index] = joined == "" ? "" : joined;

            if (!string.IsNullOrEmpty(a.Hint))
            {
                GUILayout.Space(4f);
                GUILayout.Label(vals[index] == "" ? a.HintText : "", Theme.DimLabel, GUILayout.Width(120f));
            }
        }

        private static void DrawPosOrVoid(Cmd cmd, int index, string[] vals)
        {
            string key = cmd.Name + "#" + index;
            int mode;
            if (!PosModes.TryGetValue(key, out mode)) mode = 0;

            int want = mode;
            if (GUILayout.Toggle(mode == 0, L.MyPosition, Theme.Pill, GUILayout.Width(88f)) && mode != 0) want = 0;
            if (GUILayout.Toggle(mode == 1, L.Coords, Theme.Pill, GUILayout.Width(58f)) && mode != 1) want = 1;
            if (GUILayout.Toggle(mode == 2, L.Void, Theme.Pill, GUILayout.Width(58f)) && mode != 2) want = 2;

            if (want != mode)
            {
                // 切換模式會增減後面那三個座標框，屬於結構變動
                int m = want;
                Defer(() =>
                {
                    PosModes[key] = m;
                    vals[index] = m == 0 ? "" : (m == 2 ? "void" : "0 50 0");
                });
            }

            if (mode == 1)
            {
                GUILayout.Space(4f);
                string[] xyz = SplitVec(vals[index]);
                for (int k = 0; k < 3; k++) xyz[k] = GUILayout.TextField(xyz[k], GUILayout.Width(52f));
                vals[index] = (xyz[0] + " " + xyz[1] + " " + xyz[2]).Trim();
            }
        }

        private static string[] SplitVec(string v)
        {
            string[] xyz = { "", "", "" };
            if (string.IsNullOrEmpty(v) || v.Equals("void", StringComparison.OrdinalIgnoreCase)) return xyz;
            string[] parts = v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < 3 && i < parts.Length; i++) xyz[i] = parts[i];
            return xyz;
        }

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.Output, Theme.Head);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L.Clear, GUILayout.Width(66f))) Defer(Log.Clear);
            GUILayout.EndHorizontal();

            _logScroll = GUILayout.BeginScrollView(_logScroll, Theme.Inset, GUILayout.Height(104f));
            for (int i = 0; i < _logView.Length; i++) GUILayout.Label(_logView[i], Theme.Mono);
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.Console, Theme.ArgLabel, GUILayout.Width(64f));
            GUILayout.Space(4f);
            GUI.SetNextControlName(ConsoleControl);
            _console = GUILayout.TextField(_console);

            bool enter = Event.current.type == EventType.KeyDown
                         && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                         && GUI.GetNameOfFocusedControl() == ConsoleControl;

            if (GUILayout.Button(L.Send, Theme.Run, GUILayout.Width(66f)) || enter)
            {
                if (enter) Event.current.Use();
                string line = _console.Trim();
                if (line.Length > 0)
                {
                    if (!line.StartsWith("/")) line = "/" + line;
                    _console = "";
                    string send = line;
                    Defer(() =>
                    {
                        History.Add(send);
                        _historyIdx = History.Count;
                        Run(send);
                    });
                }
            }

            GUI.enabled = History.Count > 0;
            if (GUILayout.Button("▲", GUILayout.Width(30f)))
                Defer(() =>
                {
                    _historyIdx = Mathf.Clamp(_historyIdx - 1, 0, History.Count - 1);
                    _console = History[_historyIdx];
                });
            if (GUILayout.Button("▼", GUILayout.Width(30f)))
                Defer(() =>
                {
                    _historyIdx = Mathf.Clamp(_historyIdx + 1, 0, History.Count - 1);
                    _console = History[_historyIdx];
                });
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ 選單覆蓋層

        private static void OpenPicker(Cmd cmd, int argIndex)
        {
            _pickerCmd = cmd;
            _pickerArg = argIndex;
            _pickerSearch = "";
            _pickerScroll = Vector2.zero;
            BuildOptions(cmd.Args[argIndex], out _pickerLabels, out _pickerValues);
            RebuildPickerView();
        }

        private static void ClosePicker()
        {
            _pickerCmd = null;
            _pickerArg = -1;
            _pickerLabels = null;
            _pickerValues = null;
            PickerView.Clear();
        }

        private static void DrawPicker()
        {
            Arg a = _pickerCmd.Args[_pickerArg];

            GUILayout.BeginHorizontal();
            Color prev = GUI.contentColor;
            GUI.contentColor = Theme.Accent;
            GUILayout.Label(L.PickTitle(a.Text), Theme.Title);
            GUI.contentColor = prev;
            GUILayout.Label("／" + _pickerCmd.Name, Theme.DimLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L.Cancel, GUILayout.Width(66f))) Defer(ClosePicker);
            GUILayout.EndHorizontal();

            Theme.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Label(L.Search, Theme.ArgLabel, GUILayout.Width(64f));
            GUILayout.Space(4f);
            _pickerSearch = GUILayout.TextField(_pickerSearch ?? "");
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            float height = Mathf.Max(260f, _rect.height - 150f);
            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll, Theme.Inset, GUILayout.Height(height));

            if (PickerView.Count == 0)
            {
                GUILayout.Label(L.NoMatches, Theme.DimLabel);
            }
            else
            {
                for (int n = 0; n < PickerView.Count; n++)
                {
                    int i = PickerView[n];
                    if (GUILayout.Button(_pickerLabels[i], Theme.ListItem))
                    {
                        Cmd c = _pickerCmd;
                        int arg = _pickerArg;
                        string value = _pickerValues[i];
                        Defer(() =>
                        {
                            ValuesFor(c)[arg] = value;
                            ClosePicker();
                        });
                    }
                }
            }

            GUILayout.EndScrollView();
        }

        private static void BuildOptions(Arg a, out string[] labels, out string[] values)
        {
            switch (a.Kind)
            {
                case ArgKind.Item:
                {
                    var l = new List<string>();
                    var v = new List<string>();
                    foreach (ItemEntry it in GameData.Items)
                    {
                        l.Add(it.Id.ToString().PadRight(4) + "  " + it.Display + "   ·   " + it.KindText + "   ·   " + it.Key);
                        v.Add(it.Key);
                    }
                    labels = l.ToArray(); values = v.ToArray();
                    return;
                }
                case ArgKind.Bait:       FromTable(GameData.Baits, GameData.BaitMin, out labels, out values); return;
                case ArgKind.Pocket:     FromTable(GameData.Pockets, GameData.PocketMin, out labels, out values); return;
                case ArgKind.Attachment: FromTable(GameData.Attachments, 0, out labels, out values); return;
                case ArgKind.Motor:      FromTable(GameData.Motors, 0, out labels, out values); return;
                case ArgKind.Npc:        FromTable(GameData.Npcs, 0, out labels, out values); return;
                case ArgKind.Roulette:   FromTable(GameData.RouletteColors, 0, out labels, out values); return;

                case ArgKind.Player:
                {
                    var l = new List<string>();
                    var v = new List<string>();
                    l.Add(L.Me); v.Add("me");
                    if (a.AllowAll) { l.Add(L.AllPlayersCapped); v.Add("all"); }
                    if (a.AllowNone) { l.Add(L.NoDriver); v.Add("none"); }
                    try
                    {
                        for (int i = 0; i < PlayerManager.Players.Count; i++)
                        {
                            Player p = PlayerManager.Players[i];
                            if (!p) continue;
                            l.Add(i + "   " + p.SteamName);
                            v.Add(i.ToString());
                        }
                    }
                    catch (Exception) { /* 還沒進遊戲 */ }
                    labels = l.ToArray(); values = v.ToArray();
                    return;
                }
                default:
                    labels = new string[0]; values = new string[0];
                    return;
            }
        }

        private static void FromTable(string[] table, int min, out string[] labels, out string[] values)
        {
            var l = new List<string>();
            var v = new List<string>();
            for (int i = min; i < table.Length; i++)
            {
                l.Add(table[i]);
                v.Add(GameData.IndexOf(table[i]));
            }
            labels = l.ToArray(); values = v.ToArray();
        }

        private static string LabelFor(Arg a, string value)
        {
            if (string.IsNullOrEmpty(value)) return L.NotChosen;
            switch (a.Kind)
            {
                case ArgKind.Item:
                    foreach (ItemEntry it in GameData.Items)
                        if (it.Key == value) return it.Id + "  " + it.Display;
                    return value;
                case ArgKind.Player:
                    if (value == "me") return L.Me;
                    if (value == "all") return L.AllPlayers;
                    if (value == "none") return L.NoDriver;
                    try
                    {
                        int idx;
                        if (int.TryParse(value, out idx) && idx >= 0 && idx < PlayerManager.Players.Count)
                        {
                            Player p = PlayerManager.Players[idx];
                            if (p) return idx + "  " + p.SteamName;
                        }
                    }
                    catch (Exception) { }
                    return value;
                case ArgKind.Bait:       return Lookup(GameData.Baits, value);
                case ArgKind.Pocket:     return Lookup(GameData.Pockets, value);
                case ArgKind.Attachment: return Lookup(GameData.Attachments, value);
                case ArgKind.Motor:      return Lookup(GameData.Motors, value);
                case ArgKind.Npc:        return Lookup(GameData.Npcs, value);
                case ArgKind.Roulette:   return Lookup(GameData.RouletteColors, value);
                default: return value;
            }
        }

        private static string Lookup(string[] table, string value)
        {
            foreach (string row in table)
                if (GameData.IndexOf(row) == value) return row;
            return value;
        }

        // ------------------------------------------------------------------ 送出

        private static string[] ValuesFor(Cmd cmd)
        {
            string[] v;
            if (Values.TryGetValue(cmd.Name, out v) && v.Length == cmd.Args.Length) return v;

            v = new string[cmd.Args.Length];
            for (int i = 0; i < v.Length; i++) v[i] = cmd.Args[i].Default ?? "";
            Values[cmd.Name] = v;
            return v;
        }

        /// <summary>
        /// 依序組出指令字串。參數是位置相依的，所以中間留空、後面卻有值時，
        /// 中間那格要補上預設值，否則位置會整個錯開。
        /// </summary>
        private static string Build(Cmd cmd, string[] vals)
        {
            int last = -1;
            for (int i = 0; i < vals.Length; i++)
                if (!string.IsNullOrEmpty(vals[i])) last = i;

            string line = "/" + cmd.Name;
            for (int i = 0; i <= last; i++)
            {
                string v = vals[i];
                if (string.IsNullOrEmpty(v)) v = cmd.Args[i].Default;
                if (string.IsNullOrEmpty(v)) continue;
                line += " " + v;
            }
            return line;
        }

        private static void Run(string line)
        {
            PushLog("> " + line);
            try
            {
                CommandCore.IsServerCommand(line);
            }
            catch (Exception e)
            {
                PushLog(L.CommandThrew + e.Message);
                Plugin.Log.LogError(line + " -> " + e);
            }
        }
    }
}

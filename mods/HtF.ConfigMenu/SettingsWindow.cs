using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HtF.Shared;
using UnityEngine;

namespace HtF.ConfigMenu
{
    internal sealed class PluginBlock
    {
        public string Name;
        public string Guid;
        public string Version;
        public ConfigFile File;
        public ConfigEntryBase[] Entries;

        /// <summary>
        /// 這個插件自己的詞條查詢入口（HtF.Shared.Loc.Term）。
        /// 有把共用檔案編進去的 mod 才有，沒有就是 null——那就一律顯示原字串。
        /// </summary>
        public Func<string, bool, bool, string> Terms;

        /// <summary>設定名稱／section 名稱的顯示文字。</summary>
        public string Label(string raw)
        {
            if (Terms == null || string.IsNullOrEmpty(raw)) return raw;
            return Terms(raw, Loc.IsEnglish, false) ?? raw;
        }

        public string Desc(string raw, string fallback)
        {
            if (Terms == null || string.IsNullOrEmpty(raw)) return fallback;
            return Terms(raw, Loc.IsEnglish, true) ?? fallback;
        }

        /// <summary>列舉值的顯示文字。存進 .cfg 的仍然是列舉成員名稱。</summary>
        public string EnumText(object value)
        {
            if (value == null) return "";
            string s = value.ToString();
            if (Terms == null) return s;
            return Terms("enum." + s, Loc.IsEnglish, false) ?? s;
        }
    }

    /// <summary>
    /// 用反射找插件組件裡的 `HtF.Shared.Loc.Term`。
    ///
    /// ConfigMenu 刻意**不認識任何特定 mod**，也不對它們有執行期相依，所以翻譯是
    /// 一條「有就用、沒有就算了」的約定：只要那個 mod 把 `mods/Shared/Loc.cs`
    /// 編進去（Common.props 已經幫所有 HtF mod 做了），它的設定名稱就會自動雙語，
    /// 這邊一行都不用改。別人的 mod 沒有這個入口，照樣顯示它原本的字串。
    /// </summary>
    internal static class TermProvider
    {
        private static readonly Dictionary<Assembly, Func<string, bool, bool, string>> Cache =
            new Dictionary<Assembly, Func<string, bool, bool, string>>();

        internal static Func<string, bool, bool, string> For(Assembly asm)
        {
            if (asm == null) return null;

            Func<string, bool, bool, string> f;
            if (Cache.TryGetValue(asm, out f)) return f;   // 查不到也要記，免得每次重掃

            try
            {
                Type t = asm.GetType("HtF.Shared.Loc", false);
                MethodInfo m = t == null ? null : t.GetMethod("Term",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);

                if (m != null && m.ReturnType == typeof(string))
                    f = (Func<string, bool, bool, string>)Delegate.CreateDelegate(
                        typeof(Func<string, bool, bool, string>), m);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("讀取 " + asm.GetName().Name + " 的詞條表失敗：" + e.Message);
                f = null;
            }

            Cache[asm] = f;
            return f;
        }
    }

    /// <summary>
    /// 設定頁面。
    ///
    /// 沿用 DazedTools 學到的 IMGUI 規則：Unity 每幀對 OnGUI 跑多次
    /// （Layout → 輸入事件 → Repaint），任何**改變版面結構**的狀態變更都必須
    /// 延後到下一個 Layout 事件，否則 GUILayout 會取到 null 直接 NRE。
    /// </summary>
    internal static class SettingsWindow
    {
        private const int WindowId = 0x0C0F16;

        internal static bool Visible;

        private static Rect _rect = new Rect(90f, 60f, 960f, 660f);
        private static int _plugin;
        private static Vector2 _listScroll, _bodyScroll;
        private static string _search = "";
        // 過濾用的是 Layout 當下的快照。直接用 _search 的話，打字發生在
        // 鍵盤事件那一次，Repaint 畫出來的項目數就會和 Layout 算的不一致。
        private static string _searchApplied = "";
        private static Font _font;
        private static bool _fontTried;

        private static readonly List<Action> Pending = new List<Action>();
        private static List<PluginBlock> _blocks;

        // 文字框編輯中的暫存值。直接綁 BoxedValue 的話，打到一半的
        // "1." 或 "-" 會解析失敗被吃掉，根本打不出小數。
        private static readonly Dictionary<ConfigEntryBase, string> Buffers = new Dictionary<ConfigEntryBase, string>();

        // 拖曳中的滑桿值。ConfigFile.SaveOnConfigSet 預設是 true，
        // 每動一格就寫一次檔太吵，所以放開滑鼠才提交。
        private static readonly Dictionary<ConfigEntryBase, float> Sliders = new Dictionary<ConfigEntryBase, float>();

        // 正在等待使用者按鍵的那一項
        private static ConfigEntryBase _capturing;

        // ------------------------------------------------------------------ 生命週期

        internal static void Toggle()
        {
            Visible = !Visible;
            _capturing = null;
            SetMouse(Visible);
            if (Visible) Refresh();
        }

        internal static void Tick()
        {
            if (!Visible) return;
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

        private static void Defer(Action a) { Pending.Add(a); }

        /// <summary>從 BepInEx 列舉所有插件的設定項。之後新增的 mod 會自動出現。</summary>
        internal static void Refresh()
        {
            var list = new List<PluginBlock>();
            try
            {
                foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                {
                    if (info == null || info.Instance == null || info.Instance.Config == null) continue;

                    ConfigEntryBase[] entries = info.Instance.Config.GetConfigEntries();
                    if (entries == null || entries.Length == 0) continue;

                    list.Add(new PluginBlock
                    {
                        Name = info.Metadata != null ? info.Metadata.Name : info.Instance.GetType().Name,
                        Guid = info.Metadata != null ? info.Metadata.GUID : "?",
                        Version = info.Metadata != null && info.Metadata.Version != null
                            ? info.Metadata.Version.ToString() : "",
                        File = info.Instance.Config,
                        Entries = entries,
                        Terms = TermProvider.For(info.Instance.GetType().Assembly),
                    });
                }
            }
            catch (Exception e) { Plugin.Log.LogError("列舉插件設定失敗：" + e); }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _blocks = list;
            if (_plugin >= _blocks.Count) _plugin = 0;
        }

        // ------------------------------------------------------------------ 繪製

        internal static void Draw()
        {
            if (!Visible) return;

            GUI.depth = -1000;
            EnsureFont();
            Theme.Build(_font);

            if (Event.current.type == EventType.Layout)
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
                if (_blocks == null) Refresh();
                _searchApplied = _search ?? "";
            }

            // 捕捉按鍵：在任何事件都要看，不然會漏掉
            if (_capturing != null) CaptureKey();

            GUISkin prevSkin = GUI.skin;
            Matrix4x4 prevMatrix = GUI.matrix;

            GUI.skin = Theme.Skin;
            float scale = Plugin.UiScale != null ? Plugin.UiScale.Value : 1f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            _rect = GUILayout.Window(WindowId, _rect, DrawWindow, L.WindowTitle);

            GUI.matrix = prevMatrix;
            GUI.skin = prevSkin;
        }

        private static void EnsureFont()
        {
            if (_fontTried) return;
            _fontTried = true;
            string name = Plugin.FontName != null ? Plugin.FontName.Value : "";
            if (string.IsNullOrEmpty(name)) return;
            try { _font = Font.CreateDynamicFontFromOSFont(name, 13); }
            catch (Exception e) { Plugin.Log.LogWarning("載入字型 " + name + " 失敗：" + e.Message); }
        }

        private static void DrawWindow(int id)
        {
            DrawHeader();
            Theme.Separator();

            GUILayout.BeginHorizontal();
            DrawPluginList();
            GUILayout.Space(10f);
            DrawBody();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 100000, 26));
        }

        private static void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.Search, Theme.ArgLabel, GUILayout.Width(52f));
            GUILayout.Space(4f);
            // 字串本身即時更新（不影響結構），真正的過濾等下一個 Layout
            _search = GUILayout.TextField(_search ?? "", GUILayout.Width(260f));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(L.Rescan, GUILayout.Width(90f))) Defer(Refresh);
            GUILayout.Space(6f);
            if (GUILayout.Button(L.Close, GUILayout.Width(66f))) Defer(Toggle);
            GUILayout.EndHorizontal();
        }

        private static void DrawPluginList()
        {
            GUILayout.BeginVertical(GUILayout.Width(190f));
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.Height(Mathf.Max(240f, _rect.height - 96f)));

            if (_blocks != null)
            {
                for (int i = 0; i < _blocks.Count; i++)
                {
                    bool on = _plugin == i;
                    if (GUILayout.Toggle(on, _blocks[i].Name, Theme.Tab, GUILayout.Height(28f)) && !on)
                    {
                        int pick = i;
                        Defer(() => { _plugin = pick; _bodyScroll = Vector2.zero; });
                    }
                }
                if (_blocks.Count == 0) GUILayout.Label(L.NoPlugins, Theme.DimLabel);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawBody()
        {
            GUILayout.BeginVertical();

            PluginBlock block = (_blocks != null && _plugin < _blocks.Count) ? _blocks[_plugin] : null;
            if (block == null)
            {
                GUILayout.Label(L.NoneSelected, Theme.DimLabel);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            Color prev = GUI.contentColor;
            GUI.contentColor = Theme.Accent;
            GUILayout.Label(block.Name + (block.Version.Length > 0 ? ("  v" + block.Version) : ""), Theme.Title);
            GUI.contentColor = prev;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L.ResetAll, GUILayout.Width(96f)))
            {
                PluginBlock b = block;
                Defer(() => ResetAll(b));
            }
            GUILayout.Space(6f);
            if (GUILayout.Button(L.SaveFile, Theme.Run, GUILayout.Width(96f)))
            {
                PluginBlock b = block;
                Defer(() => { try { b.File.Save(); Plugin.Log.LogInfo("已寫入 " + b.File.ConfigFilePath); } catch (Exception e) { Plugin.Log.LogError(e); } });
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(block.Guid, Theme.DimLabel);

            _bodyScroll = GUILayout.BeginScrollView(_bodyScroll, GUILayout.Height(Mathf.Max(200f, _rect.height - 150f)));

            string needle = (_searchApplied ?? "").Trim();
            string section = null;
            int shown = 0;

            for (int i = 0; i < block.Entries.Length; i++)
            {
                ConfigEntryBase e = block.Entries[i];
                if (needle.Length > 0 && !Matches(block, e, needle)) continue;

                if (e.Definition.Section != section)
                {
                    section = e.Definition.Section;
                    GUILayout.Space(6f);
                    Color c = GUI.contentColor;
                    GUI.contentColor = Theme.Accent;
                    GUILayout.Label("— " + block.Label(section) + " —", Theme.Head);
                    GUI.contentColor = c;
                }

                DrawEntry(block, e);
                shown++;
            }

            if (shown == 0) GUILayout.Label(L.NoMatches, Theme.DimLabel);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 搜尋要連**翻譯後**的字一起比。我們自己的 key 存在 .cfg 裡的識別字是中文的，
        /// 只比原字串的話，英文介面下打英文會什麼都搜不到。
        /// </summary>
        private static bool Matches(PluginBlock block, ConfigEntryBase e, string needle)
        {
            if (Has(e.Definition.Key, needle)) return true;
            if (Has(e.Definition.Section, needle)) return true;
            if (Has(block.Label(e.Definition.Key), needle)) return true;
            if (Has(block.Label(e.Definition.Section), needle)) return true;

            string d = e.Description != null ? e.Description.Description : null;
            return Has(d, needle) || Has(block.Desc(e.Definition.Key, null), needle);
        }

        private static bool Has(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------ 單項

        private static void DrawEntry(PluginBlock block, ConfigEntryBase e)
        {
            GUILayout.BeginVertical(Theme.CardBox);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Label(block.Label(e.Definition.Key), Theme.Head, GUILayout.Width(240f));
            GUILayout.Space(8f);
            DrawWidget(block, e);
            GUILayout.FlexibleSpace();

            bool isDefault = e.BoxedValue != null && e.BoxedValue.Equals(e.DefaultValue);
            GUI.enabled = !isDefault;
            if (GUILayout.Button("↺", GUILayout.Width(28f)))
            {
                ConfigEntryBase entry = e;
                Defer(() => Reset(entry));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            string desc = block.Desc(e.Definition.Key,
                e.Description != null ? e.Description.Description : "");
            if (!string.IsNullOrEmpty(desc))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10f);
                GUILayout.Label(desc, Theme.DimLabel);
                GUILayout.EndHorizontal();
            }

            if (Plugin.ShowAdvanced.Value)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10f);
                string raw;
                try { raw = e.GetSerializedValue(); } catch (Exception) { raw = "?"; }
                GUILayout.Label(e.SettingType.Name + " = " + raw, Theme.Mono);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private static void DrawWidget(PluginBlock block, ConfigEntryBase e)
        {
            Type t = e.SettingType;

            if (t == typeof(bool))
            {
                bool cur = (bool)e.BoxedValue;
                bool now = GUILayout.Toggle(cur, cur ? L.On : L.Off, Theme.Pill, GUILayout.Width(56f));
                if (now != cur)
                {
                    ConfigEntryBase entry = e;
                    bool v = now;
                    Defer(() => Set(entry, v));
                }
                return;
            }

            if (t == typeof(KeyboardShortcut))
            {
                bool waiting = _capturing == e;
                if (GUILayout.Button(waiting ? L.PressAKey : e.BoxedValue.ToString(), Theme.Pill, GUILayout.Width(170f)))
                {
                    ConfigEntryBase entry = e;
                    Defer(() => _capturing = (_capturing == entry) ? null : entry);
                }
                return;
            }

            if (t.IsEnum)
            {
                Array values = Enum.GetValues(t);
                int idx = Array.IndexOf(values, e.BoxedValue);
                ConfigEntryBase ent = e;
                if (GUILayout.Button("◀", GUILayout.Width(26f)) && values.Length > 0)
                {
                    object v = values.GetValue(((idx - 1) % values.Length + values.Length) % values.Length);
                    Defer(() => Set(ent, v));
                }
                GUILayout.Label(block.EnumText(e.BoxedValue), Theme.Label, GUILayout.Width(180f));
                if (GUILayout.Button("▶", GUILayout.Width(26f)) && values.Length > 0)
                {
                    object v = values.GetValue(((idx + 1) % values.Length + values.Length) % values.Length);
                    Defer(() => Set(ent, v));
                }
                return;
            }

            float min, max;
            if (IsNumeric(t) && TryRange(e, out min, out max))
            {
                float cur = Convert.ToSingle(e.BoxedValue, CultureInfo.InvariantCulture);
                float shown;
                if (!Sliders.TryGetValue(e, out shown)) shown = cur;

                float now = GUILayout.HorizontalSlider(shown, min, max, GUILayout.Width(200f));
                if (t == typeof(int) || t == typeof(long) || t == typeof(byte)) now = Mathf.Round(now);
                Sliders[e] = now;

                GUILayout.Space(8f);
                GUILayout.Label(Format(now, t), Theme.Label, GUILayout.Width(70f));

                // 放開滑鼠才寫入，免得拖曳時每幀都寫一次 .cfg
                if (!Input.GetMouseButton(0) && Math.Abs(now - cur) > 0.0001f)
                {
                    ConfigEntryBase entry = e;
                    float v = now;
                    Defer(() => { Set(entry, Convert.ChangeType(v, entry.SettingType, CultureInfo.InvariantCulture)); Sliders.Remove(entry); });
                }
                return;
            }

            // 其餘一律用文字框（數字、字串、以及不認識的型別）
            string buf;
            if (!Buffers.TryGetValue(e, out buf))
            {
                try { buf = e.GetSerializedValue(); } catch (Exception) { buf = ""; }
                buf = buf.Trim('"');
            }

            string edited = GUILayout.TextField(buf, GUILayout.Width(240f));
            if (edited != buf)
            {
                Buffers[e] = edited;
                TryCommitText(e, edited);
            }
        }

        private static void TryCommitText(ConfigEntryBase e, string text)
        {
            Type t = e.SettingType;
            try
            {
                if (t == typeof(string)) { e.BoxedValue = text; return; }

                if (IsNumeric(t))
                {
                    // 打到一半的 "-" 或 "1." 解析不了是正常的，留在暫存區等打完
                    double d;
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return;
                    object v = Convert.ChangeType(d, t, CultureInfo.InvariantCulture);
                    if (e.Description != null && e.Description.AcceptableValues != null)
                        v = e.Description.AcceptableValues.Clamp(v);
                    e.BoxedValue = v;
                    return;
                }

                e.SetSerializedValue(text);
            }
            catch (Exception) { /* 還沒打完就是會解析失敗，不吵 */ }
        }

        private static void Set(ConfigEntryBase e, object value)
        {
            try
            {
                if (e.Description != null && e.Description.AcceptableValues != null)
                    value = e.Description.AcceptableValues.Clamp(value);
                e.BoxedValue = value;
                Buffers.Remove(e);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("設定 " + e.Definition.Key + " 失敗：" + ex.Message); }
        }

        private static void Reset(ConfigEntryBase e)
        {
            try { e.BoxedValue = e.DefaultValue; }
            catch (Exception ex) { Plugin.Log.LogWarning(ex.Message); }
            Buffers.Remove(e);
            Sliders.Remove(e);
        }

        private static void ResetAll(PluginBlock block)
        {
            for (int i = 0; i < block.Entries.Length; i++) Reset(block.Entries[i]);
            Plugin.Log.LogInfo(block.Name + " 的設定已全部重設。");
        }

        private static void CaptureKey()
        {
            Event ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown || ev.keyCode == KeyCode.None) return;

            ConfigEntryBase target = _capturing;
            KeyCode key = ev.keyCode;
            ev.Use();

            Defer(() =>
            {
                _capturing = null;
                if (key == KeyCode.Escape) return; // Esc = 取消
                Set(target, new KeyboardShortcut(key));
            });
        }

        // ------------------------------------------------------------------ 小工具

        private static bool IsNumeric(Type t)
        {
            return t == typeof(int) || t == typeof(float) || t == typeof(double)
                || t == typeof(long) || t == typeof(short) || t == typeof(byte);
        }

        /// <summary>
        /// 從 AcceptableValueRange&lt;T&gt; 取出上下界。
        /// 它是泛型，直接轉型要枚舉所有 T，用反射讀屬性簡單得多。
        /// </summary>
        private static bool TryRange(ConfigEntryBase e, out float min, out float max)
        {
            min = 0f; max = 1f;
            if (e.Description == null || e.Description.AcceptableValues == null) return false;

            object av = e.Description.AcceptableValues;
            PropertyInfo pMin = av.GetType().GetProperty("MinValue");
            PropertyInfo pMax = av.GetType().GetProperty("MaxValue");
            if (pMin == null || pMax == null) return false;

            try
            {
                min = Convert.ToSingle(pMin.GetValue(av, null), CultureInfo.InvariantCulture);
                max = Convert.ToSingle(pMax.GetValue(av, null), CultureInfo.InvariantCulture);
            }
            catch (Exception) { return false; }

            return max > min;
        }

        private static string Format(float v, Type t)
        {
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte))
                return Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

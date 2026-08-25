using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HtF.ConfigMenu
{
    /// <summary>按鈕要插在選單那一欄的哪個位置。</summary>
    internal enum ButtonSlot
    {
        /// <summary>插在最後一顆（通常是「離開」）的上面。</summary>
        AboveLast,
        /// <summary>插在整欄最下面。</summary>
        Bottom,
        /// <summary>插在整欄最上面。</summary>
        Top,
    }

    /// <summary>
    /// 在 ESC 暫停選單和主選單各插一顆「模組設定」按鈕。
    ///
    /// 做法是**複製一顆現有的按鈕**再改文字與 onClick，而不是自己從零建 UI——
    /// 這樣字型、配色、hover 音效、選取特效全部自動跟遊戲一致，
    /// 排版也由原本的 LayoutGroup 接手。
    ///
    /// 怎麼找「要複製哪一顆」試過三種，這是第三版：
    ///
    ///   ✗ v1：靠 Button.onClick 的持久監聽者比對方法名（QuitButton 之類），
    ///     再限定在某個根物件底下。兩邊都失敗——主選單的 MainMenuManager._menuStuff
    ///     其實是 3D 場景物件（跟船、攝影機同一組），UI 是 CanvasManager 在管的；
    ///     暫停選單那邊則是方法名沒對上。
    ///
    ///   ~ v2：**找按鈕最多的那一欄**。把候選 Button 依 parent 分組，取子按鈕數
    ///     最多的那個 parent。找欄位是找對了，但**挑模板的那一步不是決定性的**：
    ///     候選來自 FindObjectsByType(..., FindObjectsSortMode.None)，那個順序
    ///     官方明說未定義，而 v2 直接拿「清單裡的最後一顆」當模板 —— 每次啟動
    ///     拿到的其實是同一欄裡的不同按鈕，複製品跟著它的 sibling index 插進去，
    ///     位置自然每次都不一樣。這就是「每次重開位置都不同」的成因。
    ///
    ///   ✓ v3（現在）：**整條選法都排成決定性的**。
    ///     欄內依 sibling index 排序（畫面上的實際順序），欄與欄之間用
    ///     (LayoutGroup 種類, 按鈕數, 階層路徑, parent 的 sibling index)
    ///     這組固定的鍵比大小。掃描順序再怎麼變，結果都一樣。
    ///     插入位置由「按鈕位置」設定決定，預設是最後一顆的上面。
    /// </summary>
    internal static class MenuButtons
    {
        private const string CloneName = "HtF_ConfigMenuButton";

        private static GameObject _pauseButton;
        private static GameObject _menuButton;
        private static float _nextTry;
        private static bool _dumped;

        private static string _appliedLabel;
        private static ButtonSlot _appliedSlot;

        private static FieldInfo _fPauseInstance, _fMainScreen, _fPauseHolder;
        private static bool _resolved;

        internal static void Tick()
        {
            // 場景切換會把 clone 一起銷毀，所以是持續嘗試而不是只做一次。
            if (Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 1f;

            try
            {
                if (Plugin.DumpMenus.Value && !_dumped) Dump();

                SyncSettings();

                if (Plugin.ShowPauseButton.Value) { if (!_pauseButton) InjectPause(); }
                else Remove(ref _pauseButton);

                if (Plugin.ShowMenuButton.Value) { if (!_menuButton) InjectMainMenu(); }
                else Remove(ref _menuButton);
            }
            catch (Exception e) { Plugin.Log.LogWarning("插入選單按鈕失敗：" + e.Message); }
        }

        /// <summary>設定或語言改了就跟上：文字直接重貼，位置得整顆重插才會動。</summary>
        private static void SyncSettings()
        {
            string label = Label();
            if (label != _appliedLabel)
            {
                _appliedLabel = label;
                if (_pauseButton) SetLabel(_pauseButton, label);
                if (_menuButton) SetLabel(_menuButton, label);
            }

            ButtonSlot slot = Plugin.ButtonPosition.Value;
            if (slot != _appliedSlot)
            {
                _appliedSlot = slot;
                Remove(ref _pauseButton);
                Remove(ref _menuButton);   // 這一輪後面會照新位置重插
            }
        }

        /// <summary>按鈕文字。設定留空 = 跟著語言走。</summary>
        private static string Label()
        {
            string s = Plugin.ButtonLabel != null ? Plugin.ButtonLabel.Value : null;
            return string.IsNullOrEmpty(s) || s.Trim().Length == 0 ? L.ButtonLabelDefault : s;
        }

        private static void Remove(ref GameObject clone)
        {
            if (clone) Object.Destroy(clone);
            clone = null;
        }

        private static void InjectPause()
        {
            GameObject screen = FieldObject(_fMainScreen);
            if (!screen) return;

            // 暫停選單平常是關著的，所以要連停用的物件一起掃
            Column column = BiggestColumn(screen.GetComponentsInChildren<Button>(true));
            if (column == null) return;

            _pauseButton = Clone(column, Label());
            if (_pauseButton)
                Plugin.Log.LogInfo("已在暫停選單加入按鈕（欄位 " + column.Path + "，"
                                   + column.Buttons.Count + " 顆）。");
        }

        private static void InjectMainMenu()
        {
            // 主選單的 UI 不在 MainMenuManager 底下，所以改成全場景掃描，
            // 只在主選單真的顯示著的時候做，並排除暫停選單那一塊。
            if (!MainMenuManager.IsInMenu) return;

            GameObject pauseHolder = FieldObject(_fPauseHolder);
            var candidates = new List<Button>();

            Button[] all = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Button b = all[i];
                if (!b || !b.gameObject.activeInHierarchy) continue;
                if (pauseHolder && b.transform.IsChildOf(pauseHolder.transform)) continue;
                if (!IsVisible(b.transform)) continue;
                candidates.Add(b);
            }

            Column column = BiggestColumn(candidates.ToArray());
            if (column == null) return;

            _menuButton = Clone(column, Label());
            if (_menuButton)
                Plugin.Log.LogInfo("已在主選單加入按鈕（欄位 " + column.Path + "，"
                                   + column.Buttons.Count + " 顆）。");
        }

        /// <summary>
        /// 有些畫面不是用 SetActive 收起來的，而是把 CanvasGroup 的 alpha 調成 0，
        /// 那種情況 activeInHierarchy 仍然是 true，按鈕會被誤算進候選。
        /// </summary>
        private static bool IsVisible(Transform t)
        {
            Transform p = t;
            int guard = 0;
            while (p && guard++ < 12)
            {
                CanvasGroup g = p.GetComponent<CanvasGroup>();
                if (g && (g.alpha < 0.1f || !g.blocksRaycasts)) return false;
                p = p.parent;
            }
            return true;
        }

        // ------------------------------------------------------------------ 找欄位

        /// <summary>同一個 parent 底下的一排按鈕，Buttons 已依畫面順序（sibling index）排好。</summary>
        private sealed class Column
        {
            internal Transform Parent;
            internal List<Button> Buttons;
            internal int LayoutRank;
            internal string Path;
        }

        /// <summary>
        /// 把按鈕依 parent 分組，回傳「最像主要按鈕直欄」的那一組。
        ///
        /// 比較的鍵**全部是結構上的常數**，跟掃描順序無關——這是位置能固定下來的關鍵：
        ///   1. LayoutGroup 種類：垂直的 &gt; 其他 LayoutGroup &gt; 沒有。
        ///      主選單、暫停選單的按鈕欄都是 VerticalLayoutGroup 排的。
        ///   2. 按鈕數：主選單主欄 6 顆，[Dev] 那種側欄只有 3 顆。
        ///   3. 階層路徑、parent 的 sibling index：純粹拿來打平手，讓結果唯一。
        /// </summary>
        private static Column BiggestColumn(Button[] buttons)
        {
            var groups = new Dictionary<Transform, List<Button>>();
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (!b || b.gameObject.name == CloneName) continue;
                Transform parent = b.transform.parent;
                if (!parent) continue;

                List<Button> list;
                if (!groups.TryGetValue(parent, out list)) groups[parent] = list = new List<Button>();
                list.Add(b);
            }

            Column best = null;
            foreach (KeyValuePair<Transform, List<Button>> kv in groups)
            {
                if (kv.Value.Count < 2) continue;   // 只有一顆的不太可能是主選單欄

                // 掃描順序是未定義的，所以這裡一定要自己排一次，
                // 「最後一顆」才會真的是畫面上最下面那顆。
                kv.Value.Sort(CompareBySibling);

                var col = new Column
                {
                    Parent = kv.Key,
                    Buttons = kv.Value,
                    LayoutRank = LayoutRank(kv.Key),
                    Path = Path(kv.Key),
                };
                if (Better(col, best)) best = col;
            }

            return best;
        }

        private static int CompareBySibling(Button a, Button b)
        {
            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        }

        private static int LayoutRank(Transform parent)
        {
            if (parent.GetComponent<VerticalLayoutGroup>()) return 2;
            if (parent.GetComponent<LayoutGroup>()) return 1;
            return 0;
        }

        private static bool Better(Column a, Column b)
        {
            if (b == null) return true;
            if (a.LayoutRank != b.LayoutRank) return a.LayoutRank > b.LayoutRank;
            if (a.Buttons.Count != b.Buttons.Count) return a.Buttons.Count > b.Buttons.Count;

            int byPath = string.CompareOrdinal(a.Path, b.Path);
            if (byPath != 0) return byPath < 0;
            return a.Parent.GetSiblingIndex() < b.Parent.GetSiblingIndex();
        }

        // ------------------------------------------------------------------ 複製

        private static GameObject Clone(Column column, string label)
        {
            // 模板固定取畫面上最下面那顆：主選單那一欄的第一顆常常是
            // 「繼續遊戲」之類的特例（大小或樣式不同），最後一顆通常是最普通的。
            Button source = column.Buttons[column.Buttons.Count - 1];
            int index = SlotIndex(column);

            GameObject clone = Object.Instantiate(source.gameObject, column.Parent);
            clone.name = CloneName;
            clone.transform.SetSiblingIndex(index);
            clone.transform.localScale = source.transform.localScale;

            StripLocalization(clone);
            SetLabel(clone, label);

            Button button = clone.GetComponent<Button>();
            if (button)
            {
                // 原本的 onClick 帶著 prefab 綁死的持久監聽者（例如「離開遊戲」），
                // 沒辦法逐條移除，直接換上一個全新的事件物件最乾淨。
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(SettingsWindow.Toggle);
                button.interactable = true;
            }

            clone.SetActive(true);
            _appliedLabel = label;
            _appliedSlot = Plugin.ButtonPosition.Value;
            return clone;
        }

        /// <summary>
        /// 算插入用的 sibling index。用的是**現有按鈕的實際 index**而不是 0 / childCount，
        /// 因為欄位裡常常還夾著標題、分隔線那類非按鈕物件。
        /// </summary>
        private static int SlotIndex(Column column)
        {
            List<Button> b = column.Buttons;
            switch (Plugin.ButtonPosition.Value)
            {
                case ButtonSlot.Top:
                    return b[0].transform.GetSiblingIndex();
                case ButtonSlot.Bottom:
                    return b[b.Count - 1].transform.GetSiblingIndex() + 1;
                default:
                    return b[b.Count - 1].transform.GetSiblingIndex();
            }
        }

        /// <summary>
        /// 拿掉本地化元件。留著的話，語系初始化或物件重新啟用時
        /// 它會用字典裡的原字串把我們設的文字蓋回去。
        /// 用名稱比對而不是型別，是為了把 LocalizeStringEvent 以外的變體一起處理掉。
        /// </summary>
        private static void StripLocalization(GameObject root)
        {
            MonoBehaviour[] all = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i]) continue;
                string n = all[i].GetType().Name;
                if (n.IndexOf("Localize", StringComparison.OrdinalIgnoreCase) >= 0) Object.Destroy(all[i]);
            }
        }

        private static void SetLabel(GameObject root, string label)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i]) texts[i].text = label;

            Text[] legacy = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacy.Length; i++)
                if (legacy[i]) legacy[i].text = label;
        }

        // ------------------------------------------------------------------ 診斷

        /// <summary>把目前場上的按鈕欄位印出來，遊戲改版導致抓不到時可以打開這個看。</summary>
        private static void Dump()
        {
            _dumped = true;
            Button[] all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var groups = new Dictionary<Transform, int>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i] || !all[i].transform.parent) continue;
                Transform p = all[i].transform.parent;
                int n;
                groups.TryGetValue(p, out n);
                groups[p] = n + 1;
            }
            Plugin.Log.LogInfo("=== 場上按鈕分組（parent = 數量, layout 分級）===");
            foreach (KeyValuePair<Transform, int> kv in groups)
                Plugin.Log.LogInfo("  " + Path(kv.Key) + " = " + kv.Value
                    + ", layout " + LayoutRank(kv.Key)
                    + (kv.Key.gameObject.activeInHierarchy ? "  [顯示中]" : ""));
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            Transform p = t.parent;
            int guard = 0;
            while (p && guard++ < 6) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }

        // ------------------------------------------------------------------ 反射

        private static GameObject FieldObject(FieldInfo field)
        {
            Resolve();
            if (_fPauseInstance == null || field == null) return null;
            object inst = _fPauseInstance.GetValue(null);
            if (inst == null || inst.Equals(null)) return null;
            return field.GetValue(inst) as GameObject;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _fPauseInstance = AccessTools.Field(typeof(PauseManager), "_instance");
            _fMainScreen = AccessTools.Field(typeof(PauseManager), "_mainScreen");
            _fPauseHolder = AccessTools.Field(typeof(PauseManager), "_pauseHolder");

            if (_fPauseInstance == null || _fMainScreen == null)
                Plugin.Log.LogWarning("PauseManager 的欄位對不上，暫停選單按鈕不會出現。");
        }
    }
}

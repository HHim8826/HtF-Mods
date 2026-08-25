using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HtF.ConfigMenu
{
    /// <summary>
    /// 深色主題。IMGUI 沒有樣式表，所以這裡在執行期產生貼圖並複製一份 GUISkin 來改，
    /// 不動 GUI.skin 本身——遊戲自己也可能用到 IMGUI。
    /// </summary>
    internal static class Theme
    {
        // --- 色票 ---
        internal static readonly Color Bg = Hex("1B1D21");
        internal static readonly Color Card = Hex("24272C");
        internal static readonly Color CardHi = Hex("2E333A");
        internal static readonly Color Field = Hex("141619");
        internal static readonly Color Border = Hex("3A3F46");
        internal static readonly Color Line = Hex("2C3036");
        internal static readonly Color Text = Hex("E6E8EB");
        internal static readonly Color Dim = Hex("8B939C");
        internal static readonly Color Accent = Hex("4FB3D9");
        internal static readonly Color AccentHi = Hex("7ACFF0");
        internal static readonly Color AccentBg = Hex("1F3742");
        internal static readonly Color Safe = Hex("6FCF97");
        internal static readonly Color Caution = Hex("F2C94C");
        internal static readonly Color Danger = Hex("EB5757");

        internal static GUISkin Skin;

        // 樣式
        internal static GUIStyle CardBox, Inset, Stripe, Sep, Title, Head, Label, DimLabel, WarnLabel,
                                 Badge, Pill, Tab, Run, ListItem, ArgLabel, Mono;

        private static bool _built;
        private static readonly List<Texture2D> Textures = new List<Texture2D>();

        internal static void Build(Font font)
        {
            if (_built) return;
            _built = true;

            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            if (font != null) Skin.font = font;

            // --- 基礎控件 ---
            Skin.window.normal.background = Framed(Bg, Border);
            Skin.window.onNormal.background = Skin.window.normal.background;
            Skin.window.border = One();
            Skin.window.padding = new RectOffset(12, 12, 30, 10);
            Skin.window.normal.textColor = Accent;
            Skin.window.onNormal.textColor = Accent;
            Skin.window.fontStyle = FontStyle.Bold;
            Skin.window.fontSize = 14;
            Skin.window.alignment = TextAnchor.UpperCenter;
            Skin.window.contentOffset = new Vector2(0f, 8f);

            Skin.box.normal.background = Framed(Card, Line);
            Skin.box.border = One();
            Skin.box.padding = new RectOffset(8, 8, 8, 8);
            Skin.box.normal.textColor = Text;

            Tint(Skin.button, CardHi, Border, Text,
                 hoverFill: Hex("383E46"), activeFill: AccentBg, activeBorder: Accent);
            Skin.button.padding = new RectOffset(10, 10, 4, 4);
            Skin.button.fontSize = 13;
            Skin.button.alignment = TextAnchor.MiddleCenter;

            Skin.textField.normal.background = Framed(Field, Border);
            Skin.textField.focused.background = Framed(Field, Accent);
            Skin.textField.hover.background = Framed(Field, Hex("4A5058"));
            Skin.textField.border = One();
            Skin.textField.padding = new RectOffset(6, 6, 4, 4);
            Skin.textField.normal.textColor = Text;
            Skin.textField.focused.textColor = Text;
            Skin.textField.hover.textColor = Text;
            Skin.textField.fontSize = 13;

            Skin.label.normal.textColor = Text;
            Skin.label.fontSize = 13;

            Skin.verticalScrollbar.normal.background = Solid(Field);
            Skin.verticalScrollbarThumb.normal.background = Framed(Hex("454B54"), Hex("545C66"));
            Skin.verticalScrollbarThumb.border = One();
            Skin.verticalScrollbarUpButton.normal.background = null;
            Skin.verticalScrollbarDownButton.normal.background = null;
            Skin.horizontalScrollbar.normal.background = Solid(Field);
            Skin.horizontalScrollbarThumb.normal.background = Skin.verticalScrollbarThumb.normal.background;
            Skin.horizontalScrollbarThumb.border = One();

            // --- 自訂樣式 ---
            CardBox = new GUIStyle(Skin.box)
            {
                padding = new RectOffset(0, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 6),
            };

            Inset = new GUIStyle(Skin.box)
            {
                normal = { background = Framed(Field, Line) },
                border = One(),
                padding = new RectOffset(8, 8, 6, 6),
            };

            Stripe = new GUIStyle { normal = { background = Solid(Color.white) } };

            Sep = new GUIStyle { normal = { background = Solid(Line) }, margin = new RectOffset(0, 0, 6, 6) };

            Title = new GUIStyle(Skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 16,
                normal = { textColor = Accent },
            };

            Head = new GUIStyle(Skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };

            Label = new GUIStyle(Skin.label);

            DimLabel = new GUIStyle(Skin.label) { wordWrap = true, normal = { textColor = Dim } };

            WarnLabel = new GUIStyle(Skin.label) { wordWrap = true, fontSize = 12, normal = { textColor = Caution } };

            Badge = new GUIStyle(Skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 40f,
            };

            ArgLabel = new GUIStyle(Skin.label) { fontSize = 12, normal = { textColor = Dim }, alignment = TextAnchor.MiddleRight };

            Mono = new GUIStyle(Skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = Hex("B9C0C8") } };

            // 切換用的膠囊按鈕：關 = 深色，開 = 強調色
            Pill = new GUIStyle(Skin.button) { fontSize = 12, padding = new RectOffset(8, 8, 3, 3) };
            Pill.onNormal.background = Framed(AccentBg, Accent);
            Pill.onHover.background = Framed(Hex("27424F"), AccentHi);
            Pill.onActive.background = Pill.onNormal.background;
            Pill.onNormal.textColor = AccentHi;
            Pill.onHover.textColor = AccentHi;
            Pill.onActive.textColor = AccentHi;

            Tab = new GUIStyle(Pill)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 8, 5, 5),
                fontSize = 13,
                margin = new RectOffset(0, 0, 0, 3),
            };

            Run = new GUIStyle(Skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
            Run.normal.background = Framed(AccentBg, Accent);
            Run.hover.background = Framed(Hex("2A4A59"), AccentHi);
            Run.active.background = Framed(Accent, AccentHi);
            Run.normal.textColor = AccentHi;
            Run.hover.textColor = Color.white;
            Run.active.textColor = Hex("0F1417");

            ListItem = new GUIStyle(Skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 5, 5),
                fontSize = 13,
            };
            ListItem.hover.background = Solid(CardHi);
            ListItem.hover.textColor = AccentHi;
            ListItem.active.background = Solid(AccentBg);
            ListItem.active.textColor = AccentHi;
        }

        /// <summary>畫一條 1px 的水平分隔線。</summary>
        internal static void Separator()
        {
            GUILayout.Box(GUIContent.none, Sep, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        }

        /// <summary>畫一條垂直色條（卡片左緣的風險指示）。</summary>
        internal static void VStripe(Color color, float width = 3f)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUILayout.Box(GUIContent.none, Stripe, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            GUI.color = prev;
        }

        // ------------------------------------------------------------------ 貼圖

        private static RectOffset One() { return new RectOffset(1, 1, 1, 1); }

        private static void Tint(GUIStyle s, Color fill, Color border, Color text,
                                 Color? hoverFill = null, Color? activeFill = null, Color? activeBorder = null)
        {
            s.normal.background = Framed(fill, border);
            s.hover.background = Framed(hoverFill ?? fill, border);
            s.active.background = Framed(activeFill ?? fill, activeBorder ?? border);
            s.focused.background = s.normal.background;
            s.border = One();
            s.normal.textColor = text;
            s.hover.textColor = text;
            s.active.textColor = text;
            s.focused.textColor = text;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new Color[4];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px);
            Finish(t);
            return t;
        }

        /// <summary>1px 外框 + 填色，配合 border = (1,1,1,1) 做九宮格拉伸。</summary>
        private static Texture2D Framed(Color fill, Color border)
        {
            const int n = 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? border : fill;
            t.SetPixels(px);
            Finish(t);
            return t;
        }

        private static void Finish(Texture2D t)
        {
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            t.Apply();
            Textures.Add(t); // 留著參照，免得被回收
        }

        private static Color Hex(string hex)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}

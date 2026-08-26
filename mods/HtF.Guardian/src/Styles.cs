using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HtF.Guardian
{
    /// <summary>
    /// 面板的樣式。IMGUI 沒有樣式表，所以在執行期產生貼圖、複製一份 GUISkin 來改
    /// ——**不動 <c>GUI.skin</c> 本身**，遊戲自己也可能用到 IMGUI。
    /// </summary>
    internal sealed class Styles
    {
        internal GUISkin Skin;
        internal GUIStyle Text, Dim, Head, Warn, Bad, Button, Danger, Tab, TabOn, Line, Stripe;

        private static readonly List<Texture2D> Textures = new List<Texture2D>();

        internal static void Ensure(ref Styles s, Font font)
        {
            if (s != null) return;
            s = Build(font);
        }

        private static Styles Build(Font font)
        {
            Color bg = Hex("1B1D21");
            Color border = Hex("3A3F46");
            Color card = Hex("24272C");
            Color cardHi = Hex("2E333A");
            Color line = Hex("2C3036");
            Color text = Hex("E6E8EB");
            Color dim = Hex("8B939C");
            Color accent = Hex("4FB3D9");
            Color caution = Hex("F2C94C");
            Color danger = Hex("EB5757");

            var s = new Styles();

            s.Skin = Object.Instantiate(GUI.skin);
            s.Skin.hideFlags = HideFlags.HideAndDontSave;
            if (font != null) s.Skin.font = font;

            s.Skin.window.normal.background = Framed(bg, border);
            s.Skin.window.onNormal.background = s.Skin.window.normal.background;
            s.Skin.window.border = new RectOffset(1, 1, 1, 1);
            s.Skin.window.padding = new RectOffset(0, 0, 26, 8);
            s.Skin.window.normal.textColor = accent;
            s.Skin.window.onNormal.textColor = accent;
            s.Skin.window.fontStyle = FontStyle.Bold;
            s.Skin.window.fontSize = 13;
            s.Skin.window.alignment = TextAnchor.UpperCenter;
            s.Skin.window.contentOffset = new Vector2(0f, 6f);

            var label = new GUIStyle(s.Skin.label)
            {
                fontSize = 12,
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 0, 0),
                normal = { textColor = text },
            };
            if (font != null) label.font = font;

            s.Text = label;
            s.Dim = new GUIStyle(label) { normal = { textColor = dim } };
            s.Head = new GUIStyle(label) { fontStyle = FontStyle.Bold, normal = { textColor = accent } };
            s.Warn = new GUIStyle(label) { wordWrap = true, normal = { textColor = caution } };
            s.Bad = new GUIStyle(label) { fontStyle = FontStyle.Bold, normal = { textColor = danger } };

            s.Button = new GUIStyle(s.Skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 2, 2),
                normal = { background = Framed(card, border), textColor = text },
                hover = { background = Framed(cardHi, accent), textColor = text },
                active = { background = Framed(cardHi, accent), textColor = accent },
            };
            if (font != null) s.Button.font = font;
            s.Button.border = new RectOffset(1, 1, 1, 1);

            s.Danger = new GUIStyle(s.Button)
            {
                normal = { background = Framed(Hex("3A1F22"), danger), textColor = danger },
                hover = { background = Framed(Hex("4A2529"), danger), textColor = Color.white },
                active = { background = Framed(Hex("4A2529"), danger), textColor = Color.white },
            };
            s.Danger.border = new RectOffset(1, 1, 1, 1);

            s.Tab = new GUIStyle(s.Button)
            {
                normal = { background = Framed(bg, line), textColor = dim },
            };
            s.Tab.border = new RectOffset(1, 1, 1, 1);

            s.TabOn = new GUIStyle(s.Button)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = Framed(Hex("1F3742"), accent), textColor = accent },
                hover = { background = Framed(Hex("1F3742"), accent), textColor = accent },
                active = { background = Framed(Hex("1F3742"), accent), textColor = accent },
            };
            s.TabOn.border = new RectOffset(1, 1, 1, 1);

            s.Line = new GUIStyle { normal = { background = Solid(line) } };
            s.Stripe = new GUIStyle { normal = { background = Solid(new Color(1f, 1f, 1f, 0.035f)) } };

            return s;
        }

        // ------------------------------------------------------------------ 貼圖

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            Finish(t);
            return t;
        }

        /// <summary>中間填色、外圍一圈邊。用 3×3 加 border 1 讓九宮格拉伸只拉中間。</summary>
        private static Texture2D Framed(Color fill, Color border)
        {
            var t = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    t.SetPixel(x, y, (x == 1 && y == 1) ? fill : border);
            Finish(t);
            return t;
        }

        private static void Finish(Texture2D t)
        {
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            t.Apply();
            Textures.Add(t);
        }

        private static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }
    }
}

using System;
using System.Reflection;
using UnityEngine;

namespace HtF.AmmoCounter
{
    /// <summary>
    /// 畫剩餘子彈。
    ///
    /// 全部用 <c>GUI.*</c> 的固定 Rect 畫，**沒有用到 GUILayout**——所以
    /// `MODDING_CONTEXT.md` 第 4 節那條「結構變更要延到 Layout 事件」的規則
    /// 在這裡不適用：那條講的是 GUILayout 會把 Layout 幀算出的結構拿去給
    /// 後續事件用，固定 Rect 沒有那個狀態。這裡只在 Repaint 事件畫，
    /// 因為沒有任何可互動的控件。
    /// </summary>
    internal static class AmmoHud
    {
        private static GUIStyle _big, _small;
        private static Texture2D _white;
        private static Font _font;
        private static bool _fontTried, _stylesReady;

        // Weapon._isReloading 是私有欄位，遊戲沒有公開的「正在裝填」查詢。
        // 拿不到就只是不顯示提示字，其餘照常。
        private static FieldInfo _reloadingField;
        private static bool _reloadingFieldTried;

        private static readonly Color Normal = new Color(0.94f, 0.95f, 0.96f);
        private static readonly Color Low = new Color(1.00f, 0.71f, 0.24f);
        private static readonly Color Empty = new Color(0.93f, 0.31f, 0.29f);
        private static readonly Color Spent = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.75f);

        internal static void Draw()
        {
            if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
            if (Event.current.type != EventType.Repaint) return;

            Weapon weapon;
            try
            {
                weapon = HeldWeapon();
                if (!weapon) return;
                if (Plugin.HideWhenAds.Value && weapon.IsAds) return;
                if (Plugin.HideWhenPaused.Value &&
                    (PauseManager.IsPaused || ChatManager.IsTyping || PlayerUI.UIDisabled)) return;
            }
            catch (Exception) { return; }

            int ammo, mag;
            bool reloading;
            string weaponName;
            try
            {
                ammo = Mathf.Max(0, weapon.Ammo);
                mag = Mathf.Max(1, weapon.Attachments.AmmoPerMag);
                if (ammo > mag) mag = ammo;          // 擴充彈匣剛裝上、還沒同步過來時
                reloading = Plugin.ShowReloading.Value && IsReloading(weapon);
                weaponName = Plugin.ShowWeaponName.Value ? Name(weapon) : null;
            }
            catch (Exception) { return; }

            EnsureFont();
            EnsureStyles();
            Render(ammo, mag, reloading, weaponName);
        }

        // ------------------------------------------------------------------ 讀值

        /// <summary>本機玩家手上那把槍；沒拿槍就回 null。</summary>
        private static Weapon HeldWeapon()
        {
            Player me = Player.LocalPlayer;
            if (!me || !me.Holding) return null;

            Item held = me.Holding.HeldItem;
            // Item.Weapon 對非武器回傳 null（Item.cs:285），不必自己判型別。
            return held ? held.Weapon : null;
        }

        private static bool IsReloading(Weapon weapon)
        {
            if (!_reloadingFieldTried)
            {
                _reloadingFieldTried = true;
                _reloadingField = typeof(Weapon).GetField("_isReloading",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (_reloadingField == null)
                    Plugin.Log.LogWarning("找不到 Weapon._isReloading，裝填提示不顯示。");
            }
            if (_reloadingField == null) return false;

            try { return (bool)_reloadingField.GetValue(weapon); }
            catch (Exception) { return false; }
        }

        private static string Name(Weapon weapon)
        {
            try
            {
                string n = weapon.GetName();
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch (Exception) { /* 落到下面用物件名 */ }

            string raw = weapon.name;
            if (string.IsNullOrEmpty(raw)) return L.Unnamed;
            int i = raw.IndexOf("(Clone)", StringComparison.Ordinal);
            return (i >= 0 ? raw.Substring(0, i) : raw).Trim();
        }

        // ------------------------------------------------------------------ 繪製

        private static void EnsureFont()
        {
            if (_fontTried) return;
            _fontTried = true;
            string name = Plugin.FontName.Value;
            if (string.IsNullOrEmpty(name)) return;
            try { _font = Font.CreateDynamicFontFromOSFont(name, 24); }
            catch (Exception e) { Plugin.Log.LogWarning("載入字型 " + name + " 失敗：" + e.Message); }
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.filterMode = FilterMode.Point;
            _white.hideFlags = HideFlags.HideAndDontSave;
            _white.Apply();

            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
            };
            if (_font != null) _big.font = _font;

            _small = new GUIStyle(_big) { fontSize = 13, fontStyle = FontStyle.Normal };
        }

        private static Color ColorFor(int ammo, int mag)
        {
            if (ammo <= 0)
            {
                if (!Plugin.BlinkWhenEmpty.Value) return Empty;
                // 一秒一個來回，用 unscaledTime 才不會在暫停或慢動作時卡住。
                float t = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
                return Color.Lerp(Empty, new Color(Empty.r, Empty.g, Empty.b, 0.35f), t);
            }
            float low = Plugin.LowThreshold.Value;
            return (low > 0f && (float)ammo / mag <= low) ? Low : Normal;
        }

        /// <summary>
        /// 先量再畫。整塊的寬度取「數字列」和「圓點列」的較大者，
        /// 兩列都置中對齊，換武器（彈匣容量變）時位置才不會左右跳。
        /// </summary>
        private static void Render(int ammo, int mag, bool reloading, string weaponName)
        {
            const float pipW = 5f, pipH = 14f, pipGap = 3f;
            const float lineGap = 4f;

            Style style = Plugin.DrawStyle.Value;
            bool wantNumber = style != Style.圓點;
            bool wantPips = style != Style.數字;

            string number = Plugin.ShowMagSize.Value ? ammo + " / " + mag : ammo.ToString();

            float w = 0f, h = 0f;

            float nameH = 0f;
            if (weaponName != null)
            {
                nameH = _small.lineHeight;
                w = Mathf.Max(w, _small.CalcSize(new GUIContent(weaponName)).x);
                h += nameH + lineGap;
            }

            float numberH = 0f;
            if (wantNumber)
            {
                numberH = _big.lineHeight;
                w = Mathf.Max(w, _big.CalcSize(new GUIContent(number)).x);
                h += numberH;
            }

            // 圓點列。彈匣很大時一排會爆掉，所以超過上限就退回只畫數字。
            bool pips = wantPips && mag <= 40;
            float pipsW = mag * pipW + (mag - 1) * pipGap;
            if (pips)
            {
                if (wantNumber) h += lineGap;
                w = Mathf.Max(w, pipsW);
                h += pipH;
            }
            else if (!wantNumber)
            {
                // 樣式選了「只有圓點」但彈匣太大——還是得顯示點什麼
                wantNumber = true;
                numberH = _big.lineHeight;
                w = Mathf.Max(w, _big.CalcSize(new GUIContent(number)).x);
                h += numberH;
            }

            float hintH = 0f;
            string hint = reloading ? L.Reloading : null;
            if (hint != null)
            {
                hintH = _small.lineHeight;
                w = Mathf.Max(w, _small.CalcSize(new GUIContent(hint)).x);
                h += lineGap + hintH;
            }

            float scale = Plugin.Scale.Value;
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float x, y;
            switch (Plugin.Anchor.Value)
            {
                case Corner.左上: x = Plugin.OffsetX.Value; y = Plugin.OffsetY.Value; break;
                case Corner.右上: x = sw - w - Plugin.OffsetX.Value; y = Plugin.OffsetY.Value; break;
                case Corner.左下: x = Plugin.OffsetX.Value; y = sh - h - Plugin.OffsetY.Value; break;
                case Corner.右下: x = sw - w - Plugin.OffsetX.Value; y = sh - h - Plugin.OffsetY.Value; break;
                default:
                    x = sw * 0.5f - w * 0.5f + Plugin.OffsetX.Value;
                    y = sh * 0.5f + Plugin.OffsetY.Value;
                    break;
            }

            Color tint = ColorFor(ammo, mag);

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float ly = y;

            if (weaponName != null)
            {
                DrawText(new Rect(x, ly, w, nameH), weaponName, _small, new Color(1f, 1f, 1f, 0.7f));
                ly += nameH + lineGap;
            }

            if (wantNumber)
            {
                DrawText(new Rect(x, ly, w, numberH), number, _big, tint);
                ly += numberH;
                if (pips) ly += lineGap;
            }

            if (pips)
            {
                float px = x + (w - pipsW) * 0.5f;
                for (int i = 0; i < mag; i++)
                {
                    Rect r = new Rect(px + i * (pipW + pipGap), ly, pipW, pipH);
                    GUI.color = Shadow;
                    GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), _white);
                    GUI.color = i < ammo ? tint : Spent;
                    GUI.DrawTexture(r, _white);
                }
                ly += pipH;
            }

            if (hint != null)
            {
                ly += lineGap;
                DrawText(new Rect(x, ly, w, hintH), hint, _small, new Color(1f, 1f, 1f, 0.8f));
            }

            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        /// <summary>
        /// 描一圈黑影再畫本體。遊戲畫面是海和天空，淺色背景下純白字幾乎看不見；
        /// GUIStyle 沒有描邊，所以自己偏移畫一次。
        /// </summary>
        private static void DrawText(Rect r, string text, GUIStyle style, Color color)
        {
            GUI.color = Color.white;   // GUI.color 會和 style 的 textColor 相乘
            style.normal.textColor = Shadow;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }
    }
}

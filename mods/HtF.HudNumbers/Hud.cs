using System;
using System.Collections.Generic;
using System.Reflection;
using HtF.Shared;
using UnityEngine;

namespace HtF.HudNumbers
{
    internal static class Hud
    {
        /// <summary>
        /// 面板的一列。
        ///
        /// 標籤和數值分開存，不是先接成一整串——因為對齊要用量出來的寬度做。
        /// 原本是用固定幾個空白去湊「金錢    123」，那只在等寬的中文標籤下勉強齊；
        /// 換成英文之後 Money / Score mult. 長度差很多，湊空白一定歪。
        /// </summary>
        private struct Row
        {
            public string Label;   // null = 空白列
            public string Value;   // null = 整列就是 Label（標題或物品名）
            public bool Head;
        }

        private static GUIStyle _label, _head, _boxStyle;
        private static Texture2D _bg;
        private static Font _font;
        private static bool _fontTried, _stylesReady;

        // Item._weight 是 protected，遊戲顯示公斤時算的是 _weight * RandomizedWeight
        // （見 Creature.cs 的 inspect 文字），這裡照抄同一個算法。
        private static FieldInfo _weightField;
        private static bool _weightFieldTried;

        private static readonly List<Row> Rows = new List<Row>();

        internal static void Draw()
        {
            if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;

            Player me;
            try
            {
                me = Player.LocalPlayer;
                if (!me) return;
                if (Plugin.HideWhenPaused.Value && (PauseManager.IsPaused || ChatManager.IsTyping || PlayerUI.UIDisabled))
                    return;
            }
            catch (Exception) { return; }

            Rows.Clear();
            try { Collect(me); }
            catch (Exception e)
            {
                Rows.Clear();
                Text(L.ReadFailed + e.GetType().Name);
            }
            if (Rows.Count == 0) return;

            EnsureFont();
            EnsureStyles();
            Render();
        }

        // ------------------------------------------------------------------ 內容

        private static void Pair(string label, string value) { Rows.Add(new Row { Label = label, Value = value }); }
        private static void Text(string text) { Rows.Add(new Row { Label = text }); }
        private static void Head(string text) { Rows.Add(new Row { Label = text, Head = true }); }
        private static void Gap() { Rows.Add(new Row()); }

        private static void Collect(Player me)
        {
            if (Plugin.ShowVitals.Value) CollectVitals(me);
            if (Plugin.ShowMoney.Value) Pair(L.Money, MoneyManager.Money.ToString());
            if (Plugin.ShowHeld.Value) CollectHeld(me);
            if (Plugin.ShowLookAt.Value) CollectLookAt(me);
        }

        private static void CollectVitals(Player me)
        {
            PlayerVitals v = me.Vitals;
            if (!v) return;

            // 上限不寫死 100——用百分比反推，遊戲改動時才不會顯示錯的分母
            int max = v.HealthPercent > 0.001f ? Mathf.RoundToInt(v.Health / v.HealthPercent) : 100;
            Pair(L.Health, v.Health + " / " + max + "   (" + Pct(v.HealthPercent) + ")");
            Pair(L.Fullness, v.Fullness.ToString());

            if (v.PoisonPercent > 0.001f) Pair(L.Poison, Pct(v.PoisonPercent));
            if (v.FirePercent > 0.001f) Pair(L.Fire, Pct(v.FirePercent));
        }

        private static void CollectHeld(Player me)
        {
            Item held = me.Holding ? me.Holding.HeldItem : null;
            if (!held) return;

            Gap();
            Head(L.HeldHead);
            AddItemRows(held);
        }

        private static void CollectLookAt(Player me)
        {
            Item item = RaycastItem(me);
            if (!item) return;

            Gap();
            Head(L.AimHead);
            AddItemRows(item);
        }

        private static void AddItemRows(Item item)
        {
            Text(CleanName(item.name) + Tags(item));

            Creature c = item.Creature;
            if (c)
            {
                if (c.IsDead) Pair(L.Status, L.Dead);
                else Pair(L.Hp, c.Hp + " / " + c.MaxHp);
            }

            Pair(L.Worth, item.TotalWorth + "   (" + L.BaseWorth + " " + item.DefaultWorth + ")");

            float kg = WeightKg(item);
            if (kg > 0f) Pair(L.Weight, kg.ToString("0.###") + " kg");

            if (Math.Abs(item.Cookness) > 0.001f) Pair(L.Cookness, Pct(item.Cookness));
            if (Math.Abs(item.KillScoreMultiplier - 1f) > 0.001f)
                Pair(L.ScoreMul, "×" + item.KillScoreMultiplier.ToString("0.##"));
        }

        private static string Tags(Item item)
        {
            string s = "";
            Creature c = item.Creature;
            if (!c) return s;
            if (c.BossType > BossType.None) s += "  [BOSS]";
            if (c.IsDrip) s += "  [DRIP]";
            if (c.IsEndangered) s += "  " + L.Endangered;
            return s;
        }

        /// <summary>
        /// 從攝影機往準心射線，找出指到的 Item。
        /// ItemManager.Items 是以「每個碰撞體的 transform」為鍵建的
        /// （ItemManager.cs 的 TryAdd(collider.transform, item)），所以命中點可以直接查表；
        /// 查不到才退回往上找父物件。
        /// </summary>
        private static Item RaycastItem(Player me)
        {
            Camera cam = GameInfo.CurCamera;
            if (!cam) return null;

            int mask = GameInfo.ItemLayer | GameInfo.ItemPartLayer;
            RaycastHit hit;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit,
                                 Plugin.LookRange.Value, mask))
                return null;

            Item item;
            if (ItemManager.Items.TryGetValue(hit.transform, out item) && item) return item;

            item = hit.transform.GetComponentInParent<Item>();
            return item;
        }

        private static float WeightKg(Item item)
        {
            if (!_weightFieldTried)
            {
                _weightFieldTried = true;
                _weightField = typeof(Item).GetField("_weight",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (_weightField == null) Plugin.Log.LogWarning("找不到 Item._weight，重量不顯示。");
            }
            if (_weightField == null) return 0f;

            try { return (float)_weightField.GetValue(item) * item.RandomizedWeight; }
            catch (Exception) { return 0f; }
        }

        private static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return L.Unnamed;
            int i = n.IndexOf("(Clone)", StringComparison.Ordinal);
            return (i >= 0 ? n.Substring(0, i) : n).Trim();
        }

        private static string Pct(float f) { return Mathf.RoundToInt(f * 100f) + "%"; }

        // ------------------------------------------------------------------ 繪製

        private static void EnsureFont()
        {
            if (_fontTried) return;
            _fontTried = true;
            string name = Plugin.FontName.Value;
            if (string.IsNullOrEmpty(name)) return;
            try { _font = Font.CreateDynamicFontFromOSFont(name, 14); }
            catch (Exception e) { Plugin.Log.LogWarning("載入字型 " + name + " 失敗：" + e.Message); }
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _bg = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new Color[4];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.07f, 0.08f, 0.09f, 0.78f);
            _bg.SetPixels(px);
            _bg.filterMode = FilterMode.Point;
            _bg.hideFlags = HideFlags.HideAndDontSave;
            _bg.Apply();

            _boxStyle = new GUIStyle { normal = { background = _bg }, padding = new RectOffset(10, 10, 8, 8) };

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                wordWrap = false,
                normal = { textColor = new Color(0.90f, 0.92f, 0.94f) },
            };
            if (_font != null) _label.font = _font;

            _head = new GUIStyle(_label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.31f, 0.70f, 0.85f) },
            };
        }

        /// <summary>
        /// 先量再畫。標籤欄的寬度取所有標籤的最大值，數值一律對齊在它右邊，
        /// 這樣中文和英文都是整齊的一欄，不必為了對齊去湊空白。
        /// </summary>
        private static void Render()
        {
            const float gap = 14f;
            float scale = Plugin.Scale.Value;
            float lineH = _label.lineHeight + 2f;

            float labelW = 0f, valueW = 0f, plainW = 0f, h = 0f;
            for (int i = 0; i < Rows.Count; i++)
            {
                Row r = Rows[i];
                if (r.Label == null) { h += lineH * 0.45f; continue; }

                GUIStyle s = r.Head ? _head : _label;
                if (r.Value == null)
                {
                    plainW = Mathf.Max(plainW, s.CalcSize(new GUIContent(r.Label)).x);
                }
                else
                {
                    labelW = Mathf.Max(labelW, s.CalcSize(new GUIContent(r.Label)).x);
                    valueW = Mathf.Max(valueW, _label.CalcSize(new GUIContent(r.Value)).x);
                }
                h += lineH;
            }

            float w = Mathf.Max(plainW, labelW + gap + valueW) + 20f;
            h += 16f;

            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float x = Plugin.OffsetX.Value;
            float y = Plugin.OffsetY.Value;
            switch (Plugin.Anchor.Value)
            {
                case Corner.右上: x = sw - w - Plugin.OffsetX.Value; break;
                case Corner.左下: y = sh - h - Plugin.OffsetY.Value; break;
                case Corner.右下: x = sw - w - Plugin.OffsetX.Value; y = sh - h - Plugin.OffsetY.Value; break;
            }

            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);

            float ly = y + 8f;
            for (int i = 0; i < Rows.Count; i++)
            {
                Row r = Rows[i];
                if (r.Label == null) { ly += lineH * 0.45f; continue; }

                GUIStyle s = r.Head ? _head : _label;
                if (r.Value == null)
                {
                    GUI.Label(new Rect(x + 10f, ly, w - 20f, lineH), r.Label, s);
                }
                else
                {
                    GUI.Label(new Rect(x + 10f, ly, labelW, lineH), r.Label, s);
                    GUI.Label(new Rect(x + 10f + labelW + gap, ly, valueW, lineH), r.Value, _label);
                }
                ly += lineH;
            }

            GUI.matrix = prev;
        }
    }
}

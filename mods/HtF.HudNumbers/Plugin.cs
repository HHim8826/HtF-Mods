using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HtF.Shared;
using UnityEngine;

namespace HtF.HudNumbers
{
    internal enum Corner { 左上, 右上, 左下, 右下 }

    /// <summary>
    /// HUD 數值化：把血量、飽食、手上物品與準心指向的東西用數字顯示出來。
    /// 純客戶端、純唯讀——沒有任何 Harmony patch，也不送任何封包，
    /// 所以在別人的房間裡用也不會影響任何人。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.hudnumbers";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<Corner> Anchor;
        internal static ConfigEntry<int> OffsetX, OffsetY;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<string> FontName;

        internal static ConfigEntry<bool> ShowVitals, ShowMoney, ShowHeld, ShowLookAt;
        internal static ConfigEntry<float> LookRange;
        internal static ConfigEntry<bool> HideWhenPaused;

        private void Awake()
        {
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 和列舉成員名一律維持原本的中文：那些是 .cfg 的識別字，
            // 翻譯它們會讓使用者既有的值全部失效。
            Loc.Section("一般", "General");
            Loc.Section("版面", "Layout");
            Loc.Section("內容", "Contents");

            Loc.EnumValue(Corner.左上, "左上", "Top Left");
            Loc.EnumValue(Corner.右上, "右上", "Top Right");
            Loc.EnumValue(Corner.左下, "左下", "Bottom Left");
            Loc.EnumValue(Corner.右下, "右下", "Bottom Right");

            Enabled = Loc.Bind(Config, "一般", "啟用", true, "Enabled",
                "總開關。",
                "Master switch.");
            ToggleKey = Loc.Bind(Config, "一般", "開關鍵", new KeyboardShortcut(KeyCode.F6), "Toggle Key",
                "遊戲內切換顯示的按鍵。",
                "Key that shows and hides the panel in game.");
            HideWhenPaused = Loc.Bind(Config, "一般", "暫停時隱藏", true, "Hide While Paused",
                "暫停、打字、或遊戲把 UI 關掉時一併隱藏。",
                "Also hide while paused, while typing, or whenever the game hides its own UI.");

            Anchor = Loc.Bind(Config, "版面", "位置", Corner.左上, "Corner",
                "面板貼在畫面的哪一角。",
                "Which corner of the screen the panel sticks to.");
            OffsetX = Loc.Bind(Config, "版面", "水平邊距", 12, "Horizontal Margin",
                "距離該角落的水平像素。",
                "Horizontal distance from that corner, in pixels.");
            OffsetY = Loc.Bind(Config, "版面", "垂直邊距", 12, "Vertical Margin",
                "距離該角落的垂直像素。",
                "Vertical distance from that corner, in pixels.");
            Scale = Loc.Bind(Config, "版面", "縮放", 1.0f, "Scale",
                "面板縮放倍率。",
                "Scale factor of the panel.",
                new AcceptableValueRange<float>(0.5f, 2.5f));
            FontName = Loc.Bind(Config, "版面", "字型", "Microsoft JhengHei UI", "Font",
                "系統字型名稱。留空 = 用 Unity 內建字型（中文可能變方塊）。",
                "System font name. Leave empty to use the built-in Unity font (CJK text may render as boxes).");

            ShowVitals = Loc.Bind(Config, "內容", "生命與飽食", true, "Health And Fullness",
                "血量、飽食、中毒、著火的實際數值。",
                "Actual numbers for health, fullness, poison and fire.");
            ShowMoney = Loc.Bind(Config, "內容", "金錢", true, "Money",
                "目前持有金額。",
                "How much money you are carrying.");
            ShowHeld = Loc.Bind(Config, "內容", "手上物品", true, "Held Item",
                "名稱、售價、重量、熟度、分數倍率。",
                "Name, value, weight, cookedness and score multiplier.");
            ShowLookAt = Loc.Bind(Config, "內容", "準心指向", true, "Crosshair Target",
                "看向的生物／物品的血量與數值。",
                "Health and stats of whatever creature or item you are looking at.");
            LookRange = Loc.Bind(Config, "內容", "準心射線距離", 60f, "Crosshair Ray Distance",
                "公尺。",
                "Metres.",
                new AcceptableValueRange<float>(5f, 300f));

            Log.LogInfo("HUD Numbers 已載入。");
        }

        private void Update()
        {
            // 語言跟著 ConfigMenu 的設定（沒裝就跟著遊戲語系），每幀重判一次。
            // OnGUI 在 Update 之後跑，所以同一幀畫出來的文字語言一致。
            Loc.Resolve();

            if (ToggleKey.Value.IsDown()) Enabled.Value = !Enabled.Value;
        }

        private void OnGUI()
        {
            Hud.Draw();
        }
    }
}

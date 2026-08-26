using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HtF.Shared;
using UnityEngine;

namespace HtF.AmmoCounter
{
    /// <summary>面板貼在哪裡。<see cref="Corner.準心下方"/> 是唯一跟著畫面中心走的。</summary>
    internal enum Corner { 準心下方, 左上, 右上, 左下, 右下 }

    /// <summary>子彈怎麼畫。</summary>
    internal enum Style { 數字, 圓點, 數字與圓點 }

    /// <summary>
    /// 剩餘子彈顯示。
    ///
    /// **純客戶端、純唯讀，零 Harmony patch，也不送任何封包**——跟
    /// <c>HtF.HudNumbers</c> 同一條路子，在別人的房間裡用不影響任何人，
    /// 也不需要別人裝。
    ///
    /// 讀的是 <c>Weapon.Ammo</c>（目前彈匣內的子彈）與
    /// <c>Weapon.Attachments.AmmoPerMag</c>（彈匣容量，會跟著擴充彈匣配件變）。
    /// 兩個都是公開讀取器，我們只讀不寫，所以不受
    /// 「Mono 會 inline 一行 auto-property」那條規則影響
    /// （那條講的是 patch，見 `MODDING_CONTEXT.md` 第 3.2 節）。
    ///
    /// 這遊戲沒有備用彈藥的概念——`Weapon.TryRefillAmmo` 直接把彈匣填滿，
    /// 所以顯示只有「彈匣內 / 彈匣容量」，沒有第三個數字。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.ammocounter";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> HideWhenPaused, HideWhenAds;

        internal static ConfigEntry<Corner> Anchor;
        internal static ConfigEntry<int> OffsetX, OffsetY;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<string> FontName;

        internal static ConfigEntry<Style> DrawStyle;
        internal static ConfigEntry<bool> ShowMagSize, ShowWeaponName, ShowReloading;
        internal static ConfigEntry<float> LowThreshold;
        internal static ConfigEntry<bool> BlinkWhenEmpty;

        private void Awake()
        {
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 和列舉成員名一律維持原本的中文：那些是 .cfg 的識別字，
            // 翻譯它們會讓使用者既有的值全部失效。
            Loc.Section("一般", "General");
            Loc.Section("版面", "Layout");
            Loc.Section("顯示", "Display");

            Loc.EnumValue(Corner.準心下方, "準心下方", "Below Crosshair");
            Loc.EnumValue(Corner.左上, "左上", "Top Left");
            Loc.EnumValue(Corner.右上, "右上", "Top Right");
            Loc.EnumValue(Corner.左下, "左下", "Bottom Left");
            Loc.EnumValue(Corner.右下, "右下", "Bottom Right");

            Loc.EnumValue(Style.數字, "數字", "Numbers");
            Loc.EnumValue(Style.圓點, "圓點", "Pips");
            Loc.EnumValue(Style.數字與圓點, "數字與圓點", "Numbers And Pips");

            Enabled = Loc.Bind(Config, "一般", "啟用", true, "Enabled",
                "總開關。",
                "Master switch.");
            ToggleKey = Loc.Bind(Config, "一般", "開關鍵", new KeyboardShortcut(KeyCode.F10), "Toggle Key",
                "遊戲內切換顯示的按鍵。",
                "Key that shows and hides the counter in game.");
            HideWhenPaused = Loc.Bind(Config, "一般", "暫停時隱藏", true, "Hide While Paused",
                "暫停、打字、或遊戲把 UI 關掉時一併隱藏。",
                "Also hide while paused, while typing, or whenever the game hides its own UI.");
            HideWhenAds = Loc.Bind(Config, "一般", "瞄準時隱藏", false, "Hide While Aiming",
                "舉槍瞄準（ADS）時暫時隱藏，狙擊鏡的畫面才不會被擋。",
                "Hide while aiming down sights, so it does not sit on top of the scope.");

            Anchor = Loc.Bind(Config, "版面", "位置", Corner.準心下方, "Position",
                "貼在畫面的哪一角；準心下方會跟著畫面中心走。",
                "Which corner of the screen to stick to. \"Below Crosshair\" follows the screen centre instead.");
            OffsetX = Loc.Bind(Config, "版面", "水平邊距", 0, "Horizontal Offset",
                "距離該位置的水平像素。",
                "Horizontal distance from that position, in pixels.");
            OffsetY = Loc.Bind(Config, "版面", "垂直邊距", 90, "Vertical Offset",
                "距離該位置的垂直像素。",
                "Vertical distance from that position, in pixels.");
            Scale = Loc.Bind(Config, "版面", "縮放", 1.0f, "Scale",
                "整體縮放倍率。",
                "Overall scale factor.",
                new AcceptableValueRange<float>(0.5f, 3f));
            FontName = Loc.Bind(Config, "版面", "字型", "Microsoft JhengHei UI", "Font",
                "系統字型名稱。留空 = 用 Unity 內建字型（中文可能變方塊）。",
                "System font name. Leave empty to use the built-in Unity font (CJK text may render as boxes).");

            DrawStyle = Loc.Bind(Config, "顯示", "樣式", Style.數字與圓點, "Style",
                "數字、一排圓點、或兩者並列。",
                "A number, a row of pips, or both.");
            ShowMagSize = Loc.Bind(Config, "顯示", "顯示彈匣容量", true, "Show Magazine Size",
                "顯示成「7 / 8」而不是只有「7」。容量會跟著擴充彈匣配件變。",
                "Show \"7 / 8\" instead of just \"7\". The size follows the extended-magazine attachment.");
            ShowWeaponName = Loc.Bind(Config, "顯示", "顯示武器名稱", false, "Show Weapon Name",
                "在子彈數上面多一行武器名稱。",
                "Add a line with the weapon's name above the count.");
            ShowReloading = Loc.Bind(Config, "顯示", "裝填提示", true, "Reload Hint",
                "正在換彈匣時顯示提示字。",
                "Show a hint while the weapon is reloading.");
            LowThreshold = Loc.Bind(Config, "顯示", "低彈量門檻", 0.34f, "Low Ammo Threshold",
                "剩餘比例低於這個值就轉成警示色。0 = 只有空彈才變色。",
                "Turn the counter to the warning colour below this fraction of a full magazine. 0 = only when empty.",
                new AcceptableValueRange<float>(0f, 1f));
            BlinkWhenEmpty = Loc.Bind(Config, "顯示", "空彈閃爍", true, "Blink When Empty",
                "彈匣打空時閃爍。",
                "Blink once the magazine runs dry.");

            Log.LogInfo("Ammo Counter 已載入。");
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
            AmmoHud.Draw();
        }
    }
}

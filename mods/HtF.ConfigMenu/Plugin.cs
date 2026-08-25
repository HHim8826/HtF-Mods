using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;
using UnityEngine;

namespace HtF.ConfigMenu
{
    /// <summary>
    /// 遊戲內的模組設定管理頁面。
    ///
    /// 不綁定任何特定 mod——它是透過 BepInEx 的 Chainloader.PluginInfos 去列舉
    /// **所有**已載入插件的設定項，所以之後新增的 mod 會自動出現，不用改這裡。
    ///
    /// 純客戶端、只讀寫設定檔，不碰遊戲狀態也不送封包。
    /// </summary>
    [BepInPlugin(Guid, "HtF Config Menu", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.configmenu";

        /// <summary>1.0.0 的「按鈕文字」預設值。見 <see cref="MigrateButtonLabel"/>。</summary>
        private const string LegacyButtonLabel = "模組設定";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<Language> LanguageSetting;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<bool> ShowAdvanced;
        internal static ConfigEntry<bool> ShowPauseButton, ShowMenuButton, DumpMenus;
        internal static ConfigEntry<string> ButtonLabel;
        internal static ConfigEntry<ButtonSlot> ButtonPosition;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // section / key 用 L 的常數：它們是 .cfg 的識別字，永遠保持原樣。
            // Loc.Bind 綁設定的同時登記英文名稱與中英說明——一個設定一行，
            // 不會有「改了這邊忘了改那邊」的問題。
            L.Register();

            LanguageSetting = Loc.Bind(Config, L.SecUi, L.KeyLanguage, Language.Auto, "Language",
                "這個頁面和選單按鈕的語言。自動 = 跟著遊戲語系走（簡體／繁體以外都當英文）。\n"
                + "其他 HtF mod 也會跟著這一個設定走。",
                "Language of this window and of the injected menu button. Auto follows the language the game itself is set to.\n"
                + "The other HtF mods follow this one setting too.");

            ToggleKey = Loc.Bind(Config, L.SecUi, L.KeyToggle, new KeyboardShortcut(KeyCode.F9), "Toggle Key",
                "開關設定頁面的按鍵。",
                "Key that opens and closes this window.");

            UiScale = Loc.Bind(Config, L.SecUi, L.KeyScale, 1.0f, "UI Scale",
                "視窗縮放倍率。",
                "Scale factor of the window.",
                new AcceptableValueRange<float>(0.6f, 2.0f));

            FontName = Loc.Bind(Config, L.SecUi, L.KeyFont, "Microsoft JhengHei UI", "Font",
                "系統字型名稱。留空 = 用 Unity 內建字型（中文可能變方塊）。",
                "System font name. Leave empty to use the built-in Unity font (CJK text may render as boxes).");

            ShowAdvanced = Loc.Bind(Config, L.SecUi, L.KeyRaw, false, "Show Raw Values",
                "在每個設定旁邊顯示它寫進 .cfg 的實際字串，除錯用。",
                "Show the exact string each setting writes into its .cfg file. For debugging.");

            ShowPauseButton = Loc.Bind(Config, L.SecButton, L.KeyPause, true, "Show In Pause Menu",
                "在 ESC 選單插一顆「模組設定」按鈕。",
                "Add a mod settings button to the ESC pause menu.");

            ShowMenuButton = Loc.Bind(Config, L.SecButton, L.KeyMenu, true, "Show In Main Menu",
                "在主畫面插一顆「模組設定」按鈕。",
                "Add a mod settings button to the main menu.");

            // 預設留空 = 跟著語言走。填了字就一律用填的，不再自動切換。
            ButtonLabel = Loc.Bind(Config, L.SecButton, L.KeyLabel, "", "Button Label",
                "插進選單那顆按鈕上顯示的文字。留空 = 跟著上面的語言設定自動切換。"
                + "（舊版的預設值「" + LegacyButtonLabel + "」也算留空，見下面的說明。）",
                "Text shown on the injected menu button. Leave it empty to follow the language setting above. "
                + "(The old default, " + LegacyButtonLabel + ", counts as empty too.)");

            MigrateButtonLabel();

            ButtonPosition = Loc.Bind(Config, L.SecButton, L.KeySlot, ButtonSlot.AboveLast, "Button Position",
                "按鈕插在那一欄的哪個位置。",
                "Where in the menu column the button is inserted.");

            DumpMenus = Loc.Bind(Config, L.SecButton, L.KeyDump, false, "Diagnostics: Dump Button Groups",
                "把場上所有按鈕依父物件分組印進 BepInEx log。遊戲改版導致按鈕插不進去時，打開這個看實際結構。",
                "Dump every button in the scene, grouped by parent, into the BepInEx log. Turn this on if a game update breaks the injection.");

            // 這個 mod 就是語言設定的擁有者，直接讀自己的；其他 mod 才需要去借。
            Loc.LocalChoice = () => LanguageSetting.Value.ToString();

            Loc.Resolve();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Config Menu 已載入，按 " + ToggleKey.Value + " 開啟。");
        }

        /// <summary>
        /// 1.0.0 的「按鈕文字」預設值就是「模組設定」，BepInEx 早就把那一行寫進使用者的
        /// .cfg 了。改成「留空 = 跟著語言」之後，那串舊預設在檔案裡看起來就跟
        /// **使用者自己打的字**一模一樣——程式分不出來，於是照著它顯示中文，
        /// 語言設定看起來像壞掉。使用者從頭到尾沒打過任何字，卻要自己去清才會正常，
        /// 這是升級的鍋，不該丟給他。
        ///
        /// 所以：值剛好等於那串舊預設時就當成留空。這是**寫進說明的規則**不是暗招——
        /// 真的想把按鈕釘死成中文的話，填任何別的字都會被原樣尊重。
        /// </summary>
        private static void MigrateButtonLabel()
        {
            if (ButtonLabel.Value != LegacyButtonLabel) return;
            ButtonLabel.Value = "";
            Log.LogInfo("「按鈕文字」原本是舊版的預設值「" + LegacyButtonLabel
                        + "」，已改成留空＝跟著語言自動切換。要固定文字的話自己填一個就好。");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            // 語言可能來自設定，也可能跟著遊戲的語系走，所以每幀重判一次。
            // 只是幾個比較，OnGUI 之前跑完，這一幀的文字就都是同一種語言。
            Loc.Resolve();

            if (ToggleKey.Value.IsDown()) SettingsWindow.Toggle();
            SettingsWindow.Tick();
            MenuButtons.Tick();
        }

        private void OnGUI()
        {
            SettingsWindow.Draw();
        }
    }

    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>頁面開著的時候把本機玩家的輸入擋掉，免得在後面亂走亂開槍。</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.BlockInputs), MethodType.Getter)]
        [HarmonyPostfix]
        private static void BlockInputs_Postfix(Player __instance, ref bool __result)
        {
            if (SettingsWindow.Visible && __instance == Player.LocalPlayer) __result = true;
        }
    }
}

using HtF.Shared;

namespace HtF.ConfigMenu
{
    /// <summary>
    /// 這個視窗自己的文字，以及自家設定的雙語登記。
    ///
    /// 底層是共用的 <see cref="Loc"/>（`mods/Shared/Loc.cs`，編譯進每個 mod）：
    /// 語言判定、詞條表、以及給 ConfigMenu 反射查用的入口都在那裡。
    /// 這裡只放「ConfigMenu 自己要顯示什麼」。
    ///
    /// 設定項的英文名稱與說明不在這個檔案——它們寫在 `Plugin.Awake` 的
    /// <see cref="Loc.Bind{T}"/> 呼叫裡，一個設定一行，key、英文名、中英說明擺在一起，
    /// 想改哪個就在那一行改，不會兩邊對不上。
    /// </summary>
    internal static class L
    {
        // --- .cfg 識別字（不要動）---
        // 「介面 / 語言」這兩個字串同時寫在 Shared/Loc.cs 的 OwnerSection / OwnerKey：
        // 其他 mod 是靠它們來借這裡的語言設定的，要改就兩邊一起改。
        internal const string SecUi = "介面";
        internal const string SecButton = "選單按鈕";

        internal const string KeyLanguage = "語言";
        internal const string KeyToggle = "開關鍵";
        internal const string KeyScale = "縮放";
        internal const string KeyFont = "字型";
        internal const string KeyRaw = "顯示原始值";

        internal const string KeyPause = "暫停選單顯示按鈕";
        internal const string KeyMenu = "主選單顯示按鈕";
        internal const string KeyLabel = "按鈕文字";
        internal const string KeySlot = "按鈕位置";
        internal const string KeyDump = "診斷：印出按鈕分組";

        /// <summary>登記 section 與列舉值的顯示文字。設定項本身由 Loc.Bind 順手登記。</summary>
        internal static void Register()
        {
            Loc.Section(SecUi, "Interface");
            Loc.Section(SecButton, "Menu Button");

            Loc.EnumValue(Language.Auto, "自動（跟隨遊戲）", "Auto (follow game)");
            Loc.EnumValue(Language.Chinese, "中文", "Chinese");
            Loc.EnumValue(Language.English, "英文", "English");

            Loc.EnumValue(ButtonSlot.AboveLast, "最後一顆的上面", "Above the last button");
            Loc.EnumValue(ButtonSlot.Bottom, "整欄最下面", "Bottom of the column");
            Loc.EnumValue(ButtonSlot.Top, "整欄最上面", "Top of the column");
        }

        // --- 視窗文字 ---

        internal static string WindowTitle { get { return Loc.P("模組設定", "Mod Settings"); } }
        internal static string ButtonLabelDefault { get { return Loc.P("模組設定", "Mod Settings"); } }

        internal static string Search { get { return Loc.P("搜尋", "Search"); } }
        internal static string Rescan { get { return Loc.P("重新掃描", "Rescan"); } }
        internal static string Close { get { return Loc.P("關閉", "Close"); } }
        internal static string ResetAll { get { return Loc.P("全部重設", "Reset All"); } }
        internal static string SaveFile { get { return Loc.P("寫入檔案", "Save File"); } }

        internal static string NoPlugins { get { return Loc.P("沒有找到任何有設定的插件", "No plugins with settings found"); } }
        internal static string NoMatches { get { return Loc.P("沒有符合的設定", "No settings match the search"); } }
        internal static string NoneSelected { get { return Loc.P("（無）", "(none)"); } }

        internal static string On { get { return Loc.P("開", "On"); } }
        internal static string Off { get { return Loc.P("關", "Off"); } }
        internal static string PressAKey { get { return Loc.P("按下按鍵…", "Press a key…"); } }
    }
}

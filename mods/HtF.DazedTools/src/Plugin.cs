using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HtF.DazedTools.UI;
using HtF.Shared;
using HarmonyLib;
using UnityEngine;

namespace HtF.DazedTools
{
    [BepInPlugin(Guid, "HtF Dazed Tools", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.dazedtools";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<bool> ForceGameCheatFlag;
        internal static ConfigEntry<bool> UnlockDangerByDefault;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 一律維持原本的中文：那是 .cfg 的識別字，翻譯它會讓舊設定全部失效。
            Loc.Section("介面", "Interface");
            Loc.Section("進階", "Advanced");

            ToggleKey = Loc.Bind(Config, "介面", "開關鍵", new KeyboardShortcut(KeyCode.Insert), "Toggle Key",
                "開關 Dazed Tools 視窗的按鍵。",
                "Key that opens and closes the Dazed Tools window.");
            UiScale = Loc.Bind(Config, "介面", "縮放", 1.0f, "UI Scale",
                "視窗縮放倍率。",
                "Scale factor of the window.",
                new AcceptableValueRange<float>(0.6f, 2.0f));
            FontName = Loc.Bind(Config, "介面", "字型", "Microsoft JhengHei UI", "Font",
                "IMGUI 用的系統字型名稱。留空 = 用 Unity 內建字型（中文可能變成方塊）。",
                "System font name used by the IMGUI window. Leave empty to use the built-in Unity font (CJK text may render as boxes).");
            ForceGameCheatFlag = Loc.Bind(Config, "進階", "開啟遊戲內建作弊鍵", false, "Enable The Game's Own Cheat Keys",
                "把 ClientSettings.CheatsEnabled 強制設為 true，會一併啟用遊戲原本的除錯熱鍵"
                + "（M/N 加減錢、O 換島、逗號跳過教學等）。預設關閉以免誤觸。",
                "Force ClientSettings.CheatsEnabled to true, which also turns on the game's own debug hotkeys "
                + "(M/N for money, O to change island, comma to skip the tutorial, and so on). Off by default so you do not trigger them by accident.");
            UnlockDangerByDefault = Loc.Bind(Config, "進階", "預設解鎖危險指令", false, "Unlock Danger Commands By Default",
                "關閉時，標為「危險」的指令要先在視窗上方打開鎖才能按。",
                "While off, commands marked DANGER stay disabled until you open the lock at the top of the window.");

            ModWindow.DangerUnlocked = UnlockDangerByDefault.Value;

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Dazed Tools 已載入，按 " + ToggleKey.Value + " 開啟視窗。");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            // 語言跟著 ConfigMenu 的設定（沒裝就跟著遊戲語系），每幀重判一次。
            // OnGUI 在 Update 之後跑，所以同一幀畫出來的文字語言一致。
            Loc.Resolve();

            if (ToggleKey.Value.IsDown()) ModWindow.Toggle();
            ModWindow.Tick();
            Commands.CommandCore.PumpBoatInput();
        }

        private void OnGUI()
        {
            ModWindow.Draw();
        }
    }
}

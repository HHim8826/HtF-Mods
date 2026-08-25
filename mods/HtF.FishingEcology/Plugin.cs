using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;

namespace HtF.FishingEcology
{
    /// <summary>
    /// 釣魚生態：重新分配抽魚的權重。
    ///
    /// 抽魚只有一個入口——CreatureManager.GetRandomItem(pos, weights)，
    /// 魚餌把自己的 BaitInfo.ItemWeights 傳進去做加權隨機。
    /// 這個 mod 在進入該方法前把權重表換成調整過的副本，
    /// **不會改到 BaitInfo 資產本身**（那是共用的 ScriptableObject，改了會髒到整個 session）。
    ///
    /// 抽魚在伺服器端執行，所以房主裝了就對全房生效。
    /// **咬鉤時間也一樣是房主專屬**：唯一讀 Bait.RandomizedCatchTime 的地方是
    /// CreatureManager.FindFishForBait（CreatureManager.cs:111），而它只從 TickUpdate
    /// 進得去，TickUpdate 只在 CreatureManager.OnStartServer 掛上 TimeManager.OnPostTick。
    /// 純客戶端裝了調它不會有任何效果。
    /// </summary>
    [BepInPlugin(Guid, "HtF Fishing Ecology", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.fishingecology";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> RareMultiplier, CommonMultiplier, BossMultiplier;
        internal static ConfigEntry<float> RareThreshold;
        internal static ConfigEntry<string> PerItem;
        internal static ConfigEntry<int> PityAfter;
        internal static ConfigEntry<float> CatchTimeMultiplier;
        internal static ConfigEntry<bool> LogRolls;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 一律維持原本的中文：那是 .cfg 的識別字，翻譯它會讓舊設定全部失效。
            Loc.Section("一般", "General");
            Loc.Section("稀有度", "Rarity");
            Loc.Section("個別調整", "Per-Item");
            Loc.Section("保底", "Pity");
            Loc.Section("咬鉤", "Bite Time");
            Loc.Section("除錯", "Debug");

            Enabled = Loc.Bind(Config, "一般", "啟用", true, "Enabled",
                "關掉就完全走遊戲原本的權重。",
                "Turn this off to fall back to the vanilla weights entirely.");

            RareThreshold = Loc.Bind(Config, "稀有度", "稀有判定門檻", 0.25f, "Rare Threshold",
                "權重小於等於「該魚餌表中最大權重 × 此值」的項目算稀有。\n"
                + "遊戲沒有魚的稀有度欄位（Rarity 只用在外觀），所以用權重本身來判定：\n"
                + "在那張表裡越難抽到的就越稀有。",
                "Anything whose weight is at or below (the largest weight in that bait's table × this value) counts as rare.\n"
                + "The game has no rarity field for fish (Rarity is only used for skins), so rarity is derived from the weights themselves:\n"
                + "the harder it is to roll in that table, the rarer it is.",
                new AcceptableValueRange<float>(0.01f, 1f));

            RareMultiplier = Loc.Bind(Config, "稀有度", "稀有魚倍率", 1.0f, "Rare Multiplier",
                "大於 1 = 稀有魚更常出現。",
                "Above 1 = rare fish show up more often.",
                new AcceptableValueRange<float>(0f, 100f));

            CommonMultiplier = Loc.Bind(Config, "稀有度", "常見魚倍率", 1.0f, "Common Multiplier",
                "調低它等於相對拉高稀有魚。",
                "Lowering this raises rare fish relative to everything else.",
                new AcceptableValueRange<float>(0f, 100f));

            BossMultiplier = Loc.Bind(Config, "稀有度", "Boss 倍率", 1.0f, "Boss Multiplier",
                "Boss 類（Creature.BossType 不是 None）的權重。\n"
                + "場上已經有 Boss 時遊戲本來就會擋掉，這裡不影響那個規則。",
                "Weight of boss creatures (Creature.BossType other than None).\n"
                + "The game already blocks a second boss while one is alive; this does not change that rule.",
                new AcceptableValueRange<float>(0f, 100f));

            PerItem = Loc.Bind(Config, "個別調整", "個別倍率", "", "Per-Item Multipliers",
                "逗號分隔的「名稱=倍率」，名稱用去空格全小寫的形式（跟 /spawn 一樣）。\n"
                + "例：tuna=5, giantpiranha=0.2, flyingfish=3\n"
                + "個別倍率會覆蓋上面的稀有／常見／Boss 倍率。",
                "Comma-separated name=multiplier pairs. Names are lowercase with spaces removed (same form /spawn takes).\n"
                + "Example: tuna=5, giantpiranha=0.2, flyingfish=3\n"
                + "A per-item multiplier overrides the rare / common / boss multipliers above.");

            PityAfter = Loc.Bind(Config, "保底", "連續幾次沒稀有就保底", 0, "Pity After N Non-Rare Rolls",
                "0 = 關閉。設 N 表示連續 N 次抽到非稀有後，下一次只從稀有項目裡抽。\n"
                + "計數是全房共用的，不是每個玩家各自計算。",
                "0 = off. Set N so that after N non-rare rolls in a row, the next roll draws only from the rare items.\n"
                + "The counter is shared by the whole lobby, not tracked per player.",
                new AcceptableValueRange<int>(0, 100));

            CatchTimeMultiplier = Loc.Bind(Config, "咬鉤", "咬鉤時間倍率", 1.0f, "Bite Time Multiplier",
                "小於 1 = 魚咬鉤更快。這一項是改 BaitInfo 資產（離開遊戲時會還原）。\n"
                + "跟權重一樣是房主專屬：唯一的讀取點只在伺服器端跑，純客戶端調它不會有效果。",
                "Below 1 = fish bite sooner. This one edits the BaitInfo asset (restored when you quit).\n"
                + "Like the weights it is host-only: the value is read server-side, so tuning it as a pure client does nothing.",
                new AcceptableValueRange<float>(0.05f, 10f));

            LogRolls = Loc.Bind(Config, "除錯", "記錄每次抽取", false, "Log Every Roll",
                "把每次抽到什麼寫進 BepInEx log，調倍率時很有用。",
                "Write every roll to the BepInEx log. Handy while tuning multipliers.");

            Config.SettingChanged += (s, e) => BaitTuner.Apply();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Fishing Ecology 已載入。");
        }

        private void Update()
        {
            BaitTuner.TickTryApply();
        }

        private void OnDestroy()
        {
            BaitTuner.Restore();
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;

namespace HtF.HostRules
{
    internal enum Force { 不變, 強制開啟, 強制關閉 }

    /// <summary>
    /// 房主規則擴充：難度、規則開關、玩家數值，以及釣魚生態。
    ///
    /// 只有房主需要裝——所有生效點都在伺服器端（傷害結算、飽食／回血 tick、抽魚）。
    /// 遊戲在 1.0.7 到 1.0.9 之間加進來的難度系統，把 HealthMultiplier / DamageMultiplier
    /// 做成 ServerSettings 上的靜態屬性，這個 mod 就接在那裡，等於把三段式難度換成無段式。
    ///
    /// 釣魚那半原本在 <c>HtF.Economy</c>。搬過來的理由是它從來就不屬於「經濟」：
    /// 抽魚權重、保底、咬鉤時間都是**房主替整個房間定的規則**，
    /// 生效條件跟難度乘數一模一樣（唯一的讀取點只在伺服器端跑），
    /// 分成兩個 mod 只是讓房主要開兩個設定頁面調同一件事。
    /// Economy 的金錢那半（賣價／花費／起始金錢）連同它一起移除了。
    ///
    /// 做不到的事：新增 SyncVar。FishNet 的同步欄位是編譯期 IL weaving 產生的，
    /// Harmony 無法補上，所以這裡只做「伺服器端算出來的結果」，不做新的同步狀態。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.hostrules";

        /// <summary>釣魚那半的前身。它們還在的話會重複 patch，見 <see cref="CheckForOldMods"/>。</summary>
        private static readonly string[] OldFishingGuids = { "htf.economy", "htf.fishingecology" };

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // 難度乘數
        internal static ConfigEntry<bool> OverrideDifficulty;
        internal static ConfigEntry<float> CreatureHealthMul, PlayerDamageTakenMul;

        // 規則開關
        internal static ConfigEntry<Force> FriendlyFire, OneShot;

        // 玩家數值（倍率作用在 prefab 預設值上）
        internal static ConfigEntry<float> HungerSpeedMul, HungerDamageMul;
        internal static ConfigEntry<float> RegenSpeedMul, RegenAmountMul;
        internal static ConfigEntry<float> PoisonDamageMul, FireDamageMul;
        internal static ConfigEntry<float> PvpDamageMul;
        internal static ConfigEntry<int> HealthOnRes, FullnessOnRes;
        internal static ConfigEntry<float> InvulnAfterDamage;

        // 釣魚生態
        internal static ConfigEntry<bool> FishingEnabled;
        internal static ConfigEntry<float> RareMultiplier, CommonMultiplier, BossMultiplier;
        internal static ConfigEntry<float> RareThreshold;
        internal static ConfigEntry<string> PerItem;
        internal static ConfigEntry<int> PityAfter;
        internal static ConfigEntry<float> CatchTimeMultiplier;
        internal static ConfigEntry<bool> LogRolls;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 和列舉成員名一律維持原本的中文：那些是 .cfg 的識別字，
            // 翻譯它們會讓使用者既有的值全部失效。
            Loc.Section("難度乘數", "Difficulty Multipliers");
            Loc.Section("規則", "Rules");
            Loc.Section("玩家數值", "Player Stats");
            Loc.Section("釣魚", "Fishing");
            Loc.Section("除錯", "Debug");

            Loc.EnumValue(Force.不變, "不變", "Unchanged");
            Loc.EnumValue(Force.強制開啟, "強制開啟", "Force On");
            Loc.EnumValue(Force.強制關閉, "強制關閉", "Force Off");

            OverrideDifficulty = Loc.Bind(Config, "難度乘數", "覆寫難度乘數", false, "Override Difficulty Multipliers",
                "打開後，下面兩個倍率會取代遊戲的 簡單／普通／困難 三段設定。",
                "When on, the two multipliers below replace the game's Easy / Default / Hard presets.");

            CreatureHealthMul = Loc.Bind(Config, "難度乘數", "生物血量倍率", 1.0f, "Creature Health Multiplier",
                "作用於 Creature.MaxHp。遊戲原本：簡單 0.75、普通 1、困難 1.25。",
                "Applied to Creature.MaxHp. Vanilla values: Easy 0.75, Default 1, Hard 1.25.",
                new AcceptableValueRange<float>(0.1f, 10f));

            PlayerDamageTakenMul = Loc.Bind(Config, "難度乘數", "玩家受傷倍率", 1.0f, "Player Damage Taken Multiplier",
                "玩家受到的所有傷害。遊戲原本：簡單 0.5、普通 1、困難 1.25。",
                "All damage players take. Vanilla values: Easy 0.5, Default 1, Hard 1.25.",
                new AcceptableValueRange<float>(0f, 10f));

            FriendlyFire = Loc.Bind(Config, "規則", "友軍傷害", Force.不變, "Friendly Fire",
                "強制覆寫房間設定裡的友傷開關。",
                "Force the lobby's friendly fire toggle one way or the other.");

            OneShot = Loc.Bind(Config, "規則", "一擊必殺", Force.不變, "One-Shot Kills",
                "強制覆寫一擊必殺。開啟時近戰／拳頭／子彈傷害固定 99999。",
                "Force the one-shot setting. When on, melee / fists / bullets all deal a flat 99999.");

            HungerSpeedMul = Loc.Bind(Config, "玩家數值", "飢餓速度倍率", 1.0f, "Hunger Speed Multiplier",
                "大於 1 餓得更快。原本每 300 tick 掉 1 點飽食。",
                "Above 1 = you get hungry faster. Vanilla loses 1 fullness every 300 ticks.",
                new AcceptableValueRange<float>(0.1f, 10f));

            HungerDamageMul = Loc.Bind(Config, "玩家數值", "飢餓扣血倍率", 1.0f, "Starvation Damage Multiplier",
                "飽食歸零後的扣血量。原本每 150 tick 扣 5。",
                "Damage taken once fullness hits zero. Vanilla is 5 every 150 ticks.",
                new AcceptableValueRange<float>(0f, 10f));

            RegenSpeedMul = Loc.Bind(Config, "玩家數值", "回血速度倍率", 1.0f, "Regen Speed Multiplier",
                "大於 1 回得更快。原本每 100 tick 回一次。",
                "Above 1 = you heal more often. Vanilla heals once every 100 ticks.",
                new AcceptableValueRange<float>(0.1f, 10f));

            RegenAmountMul = Loc.Bind(Config, "玩家數值", "回血量倍率", 1.0f, "Regen Amount Multiplier",
                "每次回復的血量。原本 5。",
                "Health restored per heal. Vanilla is 5.",
                new AcceptableValueRange<float>(0f, 10f));

            PoisonDamageMul = Loc.Bind(Config, "玩家數值", "中毒傷害倍率", 1.0f, "Poison Damage Multiplier",
                "原本每 100 tick 扣 5。",
                "Vanilla is 5 every 100 ticks.",
                new AcceptableValueRange<float>(0f, 10f));

            FireDamageMul = Loc.Bind(Config, "玩家數值", "著火傷害倍率", 1.0f, "Fire Damage Multiplier",
                "原本每 50 tick 扣 10。",
                "Vanilla is 10 every 50 ticks.",
                new AcceptableValueRange<float>(0f, 10f));

            PvpDamageMul = Loc.Bind(Config, "玩家數值", "玩家對玩家傷害倍率", -1f, "Player vs Player Damage Multiplier",
                "絕對值覆寫，−1 = 不改。遊戲預設 0.25（玩家互打只有四分之一傷害）。",
                "Absolute override, −1 = leave alone. The game default is 0.25 (players deal a quarter damage to each other).",
                new AcceptableValueRange<float>(-1f, 10f));

            HealthOnRes = Loc.Bind(Config, "玩家數值", "復活後生命", -1, "Health On Revive",
                "−1 = 不改。遊戲預設 25。",
                "−1 = leave alone. The game default is 25.",
                new AcceptableValueRange<int>(-1, 100));

            FullnessOnRes = Loc.Bind(Config, "玩家數值", "復活後飽食", -1, "Fullness On Revive",
                "−1 = 不改。遊戲預設 10。",
                "−1 = leave alone. The game default is 10.",
                new AcceptableValueRange<int>(-1, 100));

            InvulnAfterDamage = Loc.Bind(Config, "玩家數值", "受傷後無敵秒數", -1f, "Invulnerability After Damage",
                "−1 = 不改。遊戲預設 0.25。",
                "Seconds. −1 = leave alone. The game default is 0.25.",
                new AcceptableValueRange<float>(-1f, 5f));

            BindFishing();

            // 改設定後立刻套用，不用重開房間
            Config.SettingChanged += (s, e) =>
            {
                RuleApplier.ApplyAll();
                BaitTuner.Apply();
            };

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));
            _harmony.PatchAll(typeof(FishingPatches));
            Patches.VerifyTargets();

            Log.LogInfo("Host Rules 已載入（釣魚生態 " + (FishingEnabled.Value ? "開" : "關") + "）。");
        }

        /// <summary>
        /// 釣魚生態。key 沿用它在 <c>HtF.Economy</c> 時的原名——
        /// GUID 換了設定檔本來就會重生，但名字一致，文件和截圖才不會對不上。
        /// </summary>
        private void BindFishing()
        {
            FishingEnabled = Loc.Bind(Config, "釣魚", "啟用釣魚生態", true, "Enable Fishing Ecology",
                "關掉就完全走遊戲原本的抽魚權重。不影響上面的難度與玩家數值。",
                "Turn this off to fall back to the vanilla catch weights entirely. Does not affect the difficulty and player stats above.");

            RareThreshold = Loc.Bind(Config, "釣魚", "稀有判定門檻", 0.25f, "Rare Threshold",
                "權重小於等於「該魚餌表中最大權重 × 此值」的項目算稀有。\n"
                + "遊戲沒有魚的稀有度欄位（Rarity 只用在外觀），所以用權重本身來判定：\n"
                + "在那張表裡越難抽到的就越稀有。",
                "Anything whose weight is at or below (the largest weight in that bait's table × this value) counts as rare.\n"
                + "The game has no rarity field for fish (Rarity is only used for skins), so rarity is derived from the weights themselves:\n"
                + "the harder it is to roll in that table, the rarer it is.",
                new AcceptableValueRange<float>(0.01f, 1f));

            RareMultiplier = Loc.Bind(Config, "釣魚", "稀有魚倍率", 1.0f, "Rare Multiplier",
                "大於 1 = 稀有魚更常出現。",
                "Above 1 = rare fish show up more often.",
                new AcceptableValueRange<float>(0f, 100f));

            CommonMultiplier = Loc.Bind(Config, "釣魚", "常見魚倍率", 1.0f, "Common Multiplier",
                "調低它等於相對拉高稀有魚。",
                "Lowering this raises rare fish relative to everything else.",
                new AcceptableValueRange<float>(0f, 100f));

            BossMultiplier = Loc.Bind(Config, "釣魚", "Boss 倍率", 1.0f, "Boss Multiplier",
                "Boss 類（Creature.BossType 不是 None）的權重。\n"
                + "場上已經有 Boss 時遊戲本來就會擋掉，這裡不影響那個規則。",
                "Weight of boss creatures (Creature.BossType other than None).\n"
                + "The game already blocks a second boss while one is alive; this does not change that rule.",
                new AcceptableValueRange<float>(0f, 100f));

            PerItem = Loc.Bind(Config, "釣魚", "個別倍率", "", "Per-Item Multipliers",
                "逗號分隔的「名稱=倍率」，名稱用去空格全小寫的形式（跟 /spawn 一樣）。\n"
                + "例：tuna=5, giantpiranha=0.2, flyingfish=3\n"
                + "個別倍率會覆蓋上面的稀有／常見／Boss 倍率。",
                "Comma-separated name=multiplier pairs. Names are lowercase with spaces removed (same form /spawn takes).\n"
                + "Example: tuna=5, giantpiranha=0.2, flyingfish=3\n"
                + "A per-item multiplier overrides the rare / common / boss multipliers above.");

            PityAfter = Loc.Bind(Config, "釣魚", "連續幾次沒稀有就保底", 0, "Pity After N Non-Rare Rolls",
                "0 = 關閉。設 N 表示連續 N 次抽到非稀有後，下一次只從稀有項目裡抽。\n"
                + "計數是全房共用的，不是每個玩家各自計算。",
                "0 = off. Set N so that after N non-rare rolls in a row, the next roll draws only from the rare items.\n"
                + "The counter is shared by the whole lobby, not tracked per player.",
                new AcceptableValueRange<int>(0, 100));

            CatchTimeMultiplier = Loc.Bind(Config, "釣魚", "咬鉤時間倍率", 1.0f, "Bite Time Multiplier",
                "小於 1 = 魚咬鉤更快。這一項是改 BaitInfo 資產（離開遊戲時會還原）。",
                "Below 1 = fish bite sooner. This one edits the BaitInfo asset (restored when you quit).",
                new AcceptableValueRange<float>(0.05f, 10f));

            LogRolls = Loc.Bind(Config, "除錯", "記錄每次抽取", false, "Log Every Roll",
                "把每次抽到什麼寫進 BepInEx log，調倍率時很有用。",
                "Write every roll to the BepInEx log. Handy while tuning multipliers.");
        }

        private bool _checkedForOldMods;

        private void Update()
        {
            if (!_checkedForOldMods) CheckForOldMods();
            BaitTuner.TickTryApply();
        }

        /// <summary>
        /// 舊的 <c>HtF.Economy</c> 或 <c>HtF.FishingEcology</c> 還在的話大聲說一次。
        ///
        /// 三者都會 patch <c>CreatureManager.GetRandomItem</c>，而且都是把 <c>ref weights</c>
        /// 換成自己算的副本——同時載入時倍率等於**疊乘**，而且沒有任何錯誤訊息。
        /// 這正是 GUID 改名／搬家時最容易踩的坑：使用者裝了新版但沒刪舊資料夾。
        ///
        /// **在 Update 而不是 Awake 檢查**：BepInEx 是邊載入邊往
        /// <c>Chainloader.PluginInfos</c> 填的，Awake 當下對方可能還沒進去。
        /// 等到第一個 Update，所有插件都載完了。
        /// </summary>
        private void CheckForOldMods()
        {
            _checkedForOldMods = true;
            for (int i = 0; i < OldFishingGuids.Length; i++)
            {
                string guid = OldFishingGuids[i];
                if (!Chainloader.PluginInfos.ContainsKey(guid)) continue;

                Log.LogWarning("偵測到舊的 " + guid + " 還裝著。釣魚生態已經併進這個 mod，"
                               + "兩份同時載入會讓抽魚權重被套用兩次（倍率變成疊乘）。"
                               + "請把它的 BepInEx/plugins 資料夾整個刪掉。");
            }
        }

        private void OnDestroy()
        {
            BaitTuner.Restore();
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

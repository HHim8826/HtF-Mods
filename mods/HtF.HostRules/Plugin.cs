using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;

namespace HtF.HostRules
{
    internal enum Force { 不變, 強制開啟, 強制關閉 }

    /// <summary>
    /// 房主規則擴充。
    ///
    /// 只有房主需要裝——所有生效點都在伺服器端（傷害結算、飽食／回血 tick）。
    /// 遊戲在 1.0.7 到 1.0.9 之間加進來的難度系統，把 HealthMultiplier / DamageMultiplier
    /// 做成 ServerSettings 上的靜態屬性，這個 mod 就接在那裡，等於把三段式難度換成無段式。
    ///
    /// 做不到的事：新增 SyncVar。FishNet 的同步欄位是編譯期 IL weaving 產生的，
    /// Harmony 無法補上，所以這裡只做「伺服器端算出來的結果」，不做新的同步狀態。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.hostrules";

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

            // 改設定後立刻套用，不用重開房間
            Config.SettingChanged += (s, e) => RuleApplier.ApplyAll();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));
            Patches.VerifyTargets();

            Log.LogInfo("Host Rules 已載入。");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

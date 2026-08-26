using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;

namespace HtF.Economy
{
    /// <summary>
    /// 經濟調整：賣價、花費、起始金錢，以及釣魚生態（抽魚權重、保底、咬鉤時間）。
    ///
    /// 這兩半原本是兩個 mod（`HtF.Economy` 和 `HtF.FishingEcology`），合併成一個。
    /// 它們本來就是同一件事的兩端——**魚是錢的來源**，調完抽魚權重卻沒調售價
    /// （或反過來）幾乎一定會得到失衡的結果，分成兩個 GUID 只是讓人要開兩個
    /// 設定頁面來調同一條曲線。而且兩邊的生效條件完全一樣：
    /// 都在伺服器端結算，**房主裝了就對全房生效**。
    ///
    /// 唯一的例外是物品售價的「顯示」——那是各自客戶端算的，只有房主裝的話，
    /// 其他人的 UI 會顯示未調整的原價（拿到的錢仍然是調整後的）。要顯示一致就大家都裝。
    ///
    /// 咬鉤時間與抽魚權重則是**純房主專屬**：唯一的讀取點只在伺服器端跑，
    /// 純客戶端調它不會有任何效果（詳見 <see cref="BaitTuner"/>）。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.economy";

        /// <summary>合併前的釣魚生態 mod。它還在的話會重複 patch，見 <see cref="CheckForOldMod"/>。</summary>
        private const string OldEcologyGuid = "htf.fishingecology";

        internal static ManualLogSource Log;

        // 金錢
        internal static ConfigEntry<float> SellMultiplier;
        internal static ConfigEntry<float> CostMultiplier;
        internal static ConfigEntry<int> StartingMoney;
        internal static ConfigEntry<bool> LogTransactions;

        // 釣魚生態
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
            //
            // 金錢那三個 section（倍率／存檔／除錯）刻意**維持原名原 key**，
            // 這樣既有的 htf.economy.cfg 直接沿用，使用者調好的值不會掉回預設。
            // 釣魚那半的 key 反正跟著舊 GUID 一起失效了，就趁機收成一個 section。
            Loc.Section("倍率", "Multipliers");
            Loc.Section("存檔", "Save Files");
            Loc.Section("釣魚", "Fishing");
            Loc.Section("除錯", "Debug");

            BindMoney();
            BindFishing();

            Config.SettingChanged += (s, e) => BaitTuner.Apply();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(MoneyPatches));
            _harmony.PatchAll(typeof(FishingPatches));

            Log.LogInfo("Economy 已載入（賣價 ×" + SellMultiplier.Value
                        + "，花費 ×" + CostMultiplier.Value
                        + "，釣魚生態 " + (Enabled.Value ? "開" : "關") + "）。");
        }

        private void BindMoney()
        {
            SellMultiplier = Loc.Bind(Config, "倍率", "賣價倍率", 1.0f, "Sell Price Multiplier",
                "所有物品的售價（Item.TotalWorth）。小於 1 = 硬核經濟，大於 1 = 輕鬆。\n"
                + "同時影響賣箱、NPC 收購，以及 UI 上顯示的價格。",
                "Sell price of every item (Item.TotalWorth). Below 1 = hardcore economy, above 1 = relaxed.\n"
                + "Affects the sell box, NPC buyers, and the price shown in the UI.",
                new AcceptableValueRange<float>(0f, 100f));

            CostMultiplier = Loc.Bind(Config, "倍率", "花費倍率", 1.0f, "Cost Multiplier",
                "所有購買行為實際扣的錢（商店、魚餌、口袋、配件、升級、賭注）。\n"
                + "注意：商店 UI 顯示的仍是原價，「買不買得起」的判斷也用原價，"
                + "只有實際扣款會乘上這個倍率（扣到 0 為止，不會變負數）。",
                "What every purchase actually costs you (shop, bait, pockets, attachments, upgrades, bets).\n"
                + "Note: the shop UI still shows the original price and the can-you-afford-it check still uses it; "
                + "only the amount actually deducted is multiplied (clamped at 0, never negative).",
                new AcceptableValueRange<float>(0f, 100f));

            StartingMoney = Loc.Bind(Config, "存檔", "新存檔起始金錢", -1, "Starting Money For New Saves",
                "−1 = 不改（遊戲預設 0）。只影響之後新建的存檔，不會動到既有存檔。",
                "−1 = leave alone (the game default is 0). Only applies to saves created from now on; existing saves are untouched.",
                new AcceptableValueRange<int>(-1, 1000000));

            LogTransactions = Loc.Bind(Config, "除錯", "記錄金錢異動", false, "Log Money Changes",
                "把每次扣款寫進 BepInEx log，方便確認倍率有沒有生效。",
                "Write every deduction to the BepInEx log, so you can confirm the multiplier is doing something.");
        }

        private void BindFishing()
        {
            Enabled = Loc.Bind(Config, "釣魚", "啟用釣魚生態", true, "Enable Fishing Ecology",
                "關掉就完全走遊戲原本的抽魚權重。不影響上面的賣價與花費倍率。",
                "Turn this off to fall back to the vanilla catch weights entirely. Does not affect the sell and cost multipliers above.");

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
                "小於 1 = 魚咬鉤更快。這一項是改 BaitInfo 資產（離開遊戲時會還原）。\n"
                + "跟權重一樣是房主專屬：唯一的讀取點只在伺服器端跑，純客戶端調它不會有效果。",
                "Below 1 = fish bite sooner. This one edits the BaitInfo asset (restored when you quit).\n"
                + "Like the weights it is host-only: the value is read server-side, so tuning it as a pure client does nothing.",
                new AcceptableValueRange<float>(0.05f, 10f));

            LogRolls = Loc.Bind(Config, "除錯", "記錄每次抽取", false, "Log Every Roll",
                "把每次抽到什麼寫進 BepInEx log，調倍率時很有用。",
                "Write every roll to the BepInEx log. Handy while tuning multipliers.");
        }

        private bool _checkedForOldMod;

        private void Update()
        {
            if (!_checkedForOldMod) CheckForOldMod();
            BaitTuner.TickTryApply();
        }

        /// <summary>
        /// 舊的 <c>HtF.FishingEcology</c> 還在的話大聲說一次。
        ///
        /// 兩個 DLL 都在時，抽魚權重會被**patch 兩次**——兩邊各自把 `ref weights`
        /// 換成自己算的副本，倍率等於疊乘，而且沒有任何錯誤訊息。
        /// 這正是 GUID 改名／合併時最容易踩的坑：使用者裝新版但沒刪舊資料夾。
        ///
        /// **在 Update 而不是 Awake 檢查**：BepInEx 是邊載入邊往
        /// <c>Chainloader.PluginInfos</c> 填的，而載入順序按 GUID 排——
        /// `htf.economy` 排在 `htf.fishingecology` 前面，在 Awake 當下對方還沒進去。
        /// 等到第一個 Update，所有插件都載完了。
        /// </summary>
        private void CheckForOldMod()
        {
            _checkedForOldMod = true;
            if (!Chainloader.PluginInfos.ContainsKey(OldEcologyGuid)) return;

            Log.LogWarning("偵測到舊的 HtF.FishingEcology 還裝著。它已經併進這個 mod，"
                           + "兩份同時載入會讓抽魚權重被套用兩次（倍率變成疊乘）。"
                           + "請把 BepInEx/plugins/HtF.FishingEcology 整個資料夾刪掉。");
        }

        private void OnDestroy()
        {
            BaitTuner.Restore();
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

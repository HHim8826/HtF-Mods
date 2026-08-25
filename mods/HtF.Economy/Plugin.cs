using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;

namespace HtF.Economy
{
    /// <summary>
    /// 經濟調整：賣價、花費、起始金錢。
    ///
    /// 金額的實際結算全在伺服器端，所以**房主裝了就生效**。
    /// 但物品售價的「顯示」是各自客戶端算的，只有房主裝的話，
    /// 其他人的 UI 會顯示未調整的原價（拿到的錢仍然是調整後的）。
    /// 要顯示一致就大家都裝。
    /// </summary>
    [BepInPlugin(Guid, "HtF Economy", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.economy";

        internal static ManualLogSource Log;

        internal static ConfigEntry<float> SellMultiplier;
        internal static ConfigEntry<float> CostMultiplier;
        internal static ConfigEntry<int> StartingMoney;
        internal static ConfigEntry<bool> LogTransactions;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 一律維持原本的中文：那是 .cfg 的識別字，翻譯它會讓舊設定全部失效。
            Loc.Section("倍率", "Multipliers");
            Loc.Section("存檔", "Save Files");
            Loc.Section("除錯", "Debug");

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

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Economy 已載入（賣價 ×" + SellMultiplier.Value + "，花費 ×" + CostMultiplier.Value + "）。");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}

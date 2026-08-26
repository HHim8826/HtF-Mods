using System;
using HarmonyLib;
using UnityEngine;

namespace HtF.Economy
{
    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class MoneyPatches
    {
        /// <summary>
        /// 售價。接在 TotalWorth 這個唯一的算式上——
        /// MoneyManager.SellItem 直接用它加錢，UI 也用它顯示，一個點就全涵蓋。
        /// 原式：_worth × 隨機重量 × 熟度曲線 × 賭注倍率 × 擊殺分數倍率。
        /// </summary>
        [HarmonyPatch(typeof(Item), nameof(Item.TotalWorth), MethodType.Getter)]
        [HarmonyPostfix]
        private static void TotalWorth_Postfix(ref int __result)
        {
            float mul = Plugin.SellMultiplier.Value;
            if (Mathf.Abs(mul - 1f) < 0.0001f) return;
            __result = Mathf.Max(0, Mathf.RoundToInt(__result * mul));
        }

        /// <summary>
        /// 花費。Server.cs 裡每一條購買 RPC 最後都走這裡
        /// （商店物品、口袋、魚餌、馬達、配件、子彈與銳利度升級、賭注），
        /// 所以改這一個點就等於改全部價格。
        /// </summary>
        [HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.RemoveMoney))]
        [HarmonyPrefix]
        private static void RemoveMoney_Prefix(ref int amount)
        {
            float mul = Plugin.CostMultiplier.Value;
            if (Mathf.Abs(mul - 1f) < 0.0001f) return;

            int before = amount;
            amount = Mathf.Max(0, Mathf.RoundToInt(Mathf.Abs(amount) * mul));
            if (Plugin.LogTransactions.Value)
                Plugin.Log.LogInfo("扣款 " + before + " -> " + amount);
        }

        /// <summary>
        /// 新建存檔時的起始金錢。
        /// MoneyManager.OnStartServer 是從 SaveManager.CurServerSave.Money 讀初始值的，
        /// 而 CreateServer 正好在最後把新的存檔物件指派給 CurServerSave，所以接在它後面。
        ///
        /// **改完記憶體還要再寫一次磁碟。** CreateServer 自己在方法內就已經把
        /// `Money = 0` 的物件序列化寫進存檔檔案了（`SaveManager.cs:80-82`），
        /// postfix 是在那之後才跑的——只改 `CurServerSave.Money` 的話，磁碟上那份
        /// 仍然是 0。房主開完新遊戲、在下一次自動存檔之前當掉或關掉遊戲，
        /// 起始金錢就這樣沒了。所以照著 CreateServer 的最後兩行再做一次。
        /// </summary>
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.CreateServer))]
        [HarmonyPostfix]
        private static void CreateServer_Postfix()
        {
            int start = Plugin.StartingMoney.Value;
            if (start < 0) return;
            if (SaveManager.CurServerSave == null) return;

            SaveManager.CurServerSave.Money = start;

            try
            {
                SaveSystem.SaveServer(SaveManager.CurServerSave.Name,
                                      JsonUtility.ToJson(SaveManager.CurServerSave));
                Plugin.Log.LogInfo("新存檔起始金錢設為 " + start + "（已寫回磁碟）。");
            }
            catch (Exception e)
            {
                // 寫不進去也不要讓建立存檔失敗：記憶體裡的值是對的，
                // 下一次自動存檔就會補上，只是中間當掉會掉回 0。
                Plugin.Log.LogWarning("新存檔起始金錢設為 " + start
                    + "，但寫回磁碟失敗：" + e.Message
                    + "。下一次存檔才會落地。");
            }
        }
    }
}

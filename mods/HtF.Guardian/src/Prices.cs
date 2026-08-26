using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HtF.Guardian
{
    /// <summary>
    /// 魚餌／船馬達／船雷達的真實售價。
    ///
    /// 這三條購買 RPC 把價格當成參數讓客戶端自己填：
    /// <c>BuyBait(player, index, cost)</c>、<c>BuyBoatMotor(player, index, cost)</c>、
    /// <c>BuyBoatRadar(player, cost)</c>。伺服器只檢查「買不買得起」，
    /// 傳 <c>cost = 0</c> 就是白拿。
    ///
    /// 正解不是硬寫價格表，而是去問場上那個販賣點：價格是
    /// <c>Purchasable._customCost</c>（序列化在場景物件上），遊戲自己
    /// (<c>BaitPurchasable.Interact</c> 等) 送出的就是這個值。
    ///
    /// **允許的是一組值而不是單一值**：同一種魚餌可能同時有付費攤位和免費攤位
    /// （<c>Purchasable._isFree</c> 會讓 <c>_customCost</c> 變 0），教學區的餌就是免費的。
    /// 所以規則是「送來的價格必須等於場上某個賣這樣東西的攤位的價格」。
    ///
    /// 查不到任何攤位時一律放行——不確定就不擋，寧可漏也不要把正常玩的人擋掉。
    /// </summary>
    internal static class Prices
    {
        private const float RebuildEvery = 15f;

        private static readonly Dictionary<int, List<int>> BaitCosts = new Dictionary<int, List<int>>();
        private static readonly Dictionary<int, List<int>> MotorCosts = new Dictionary<int, List<int>>();
        private static readonly List<int> RadarCosts = new List<int>();

        private static float _builtAt = float.NegativeInfinity;

        private static FieldInfo _customCost, _baitField, _motorIndexField;
        private static bool _fieldsTried;

        // ------------------------------------------------------------------ 對外

        /// <summary>true = 這個價格可以接受。false 時 <paramref name="expected"/> 是應該收的錢。</summary>
        internal static bool BaitCost(byte index, int cost, out int expected)
        {
            expected = cost;
            Rebuild();

            List<int> allowed;
            if (BaitCosts.TryGetValue(index, out allowed) && allowed.Count > 0)
            {
                if (allowed.Contains(cost)) return true;
                expected = Max(allowed);
                return false;
            }

            // 場上沒有這種餌的攤位——退回魚餌資產上的定價。
            int listed = ListedBaitCost(index);
            if (listed < 0) return true;
            if (cost == listed) return true;
            expected = listed;
            return false;
        }

        internal static bool MotorCost(byte index, int cost, out int expected)
        {
            expected = cost;
            Rebuild();

            List<int> allowed;
            if (!MotorCosts.TryGetValue(index, out allowed) || allowed.Count == 0) return true;
            if (allowed.Contains(cost)) return true;
            expected = Max(allowed);
            return false;
        }

        internal static bool RadarCost(int cost, out int expected)
        {
            expected = cost;
            Rebuild();

            if (RadarCosts.Count == 0) return true;
            if (RadarCosts.Contains(cost)) return true;
            expected = Max(RadarCosts);
            return false;
        }

        internal static void Invalidate() { _builtAt = float.NegativeInfinity; }

        // ------------------------------------------------------------------ 內部

        private static int Max(List<int> values)
        {
            int m = values[0];
            for (int i = 1; i < values.Count; i++) if (values[i] > m) m = values[i];
            return m;
        }

        private static int ListedBaitCost(byte index)
        {
            try
            {
                var all = GameInfo.AllBaits;
                if (all == null || index >= all.Count) return -1;
                BaitInfo b = all[index];
                return b ? b.Cost : -1;
            }
            catch (Exception) { return -1; }
        }

        /// <summary>
        /// 重掃場上的販賣點。十幾秒一次就夠——販賣點是關卡物件，只有換島／載入才會變。
        ///
        /// **只掃啟用中的物件**（<c>FindObjectsInactive.Exclude</c>）：
        /// <c>BaitPurchasable._customCost</c> 是在 <c>Awake</c> 裡從魚餌資產算出來的
        /// （`_isFree ? 0 : _bait.Cost`），沒醒過的物件上那個欄位還是序列化的預設值 0，
        /// 收進來會變成一筆假的「這種餌是免費的」，正好是我們要擋的那個洞。
        /// 玩家也沒辦法跟停用中的攤位互動，所以漏掉它們沒有代價。
        /// </summary>
        private static void Rebuild()
        {
            float now = Time.unscaledTime;
            if (now - _builtAt < RebuildEvery) return;
            _builtAt = now;

            EnsureFields();
            if (_customCost == null) return;

            BaitCosts.Clear();
            MotorCosts.Clear();
            RadarCosts.Clear();

            try
            {
                if (_baitField != null)
                {
                    foreach (BaitPurchasable p in Object.FindObjectsByType<BaitPurchasable>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (!p) continue;
                        BaitInfo bait = _baitField.GetValue(p) as BaitInfo;
                        if (!bait) continue;
                        Add(BaitCosts, GameInfo.GetIndexOfBait(bait), (int)_customCost.GetValue(p));
                    }
                }

                if (_motorIndexField != null)
                {
                    foreach (MotorPurchasable p in Object.FindObjectsByType<MotorPurchasable>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (!p) continue;
                        Add(MotorCosts, (byte)_motorIndexField.GetValue(p), (int)_customCost.GetValue(p));
                    }
                }

                foreach (BoatRadarPurchasable p in Object.FindObjectsByType<BoatRadarPurchasable>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!p) continue;
                    int c = (int)_customCost.GetValue(p);
                    if (!RadarCosts.Contains(c)) RadarCosts.Add(c);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("掃描販賣點失敗，價格檢查這一輪跳過：" + e.Message);
                BaitCosts.Clear();
                MotorCosts.Clear();
                RadarCosts.Clear();
            }
        }

        private static void Add(Dictionary<int, List<int>> into, int key, int cost)
        {
            List<int> list;
            if (!into.TryGetValue(key, out list)) into[key] = list = new List<int>(2);
            if (!list.Contains(cost)) list.Add(cost);
        }

        private static void EnsureFields()
        {
            if (_fieldsTried) return;
            _fieldsTried = true;

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _customCost = typeof(Purchasable).GetField("_customCost", F);
            _baitField = typeof(BaitPurchasable).GetField("_bait", F);
            _motorIndexField = typeof(MotorPurchasable).GetField("_motorIndex", F);

            if (_customCost == null)
                Plugin.Log.LogWarning("找不到 Purchasable._customCost，價格檢查停用（遊戲更新？）。");
            if (_baitField == null)
                Plugin.Log.LogWarning("找不到 BaitPurchasable._bait，魚餌價格改用資產定價比對。");
            if (_motorIndexField == null)
                Plugin.Log.LogWarning("找不到 MotorPurchasable._motorIndex，船馬達價格不檢查。");
        }
    }
}

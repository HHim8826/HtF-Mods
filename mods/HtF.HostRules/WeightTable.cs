using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HtF.HostRules
{
    /// <summary>
    /// 產生調整過的權重表。
    ///
    /// ItemInfoWeight 的兩個欄位都是私有的（fishable / _weight），
    /// 所以用反射建構新的實例——絕對不要改傳進來的那份，
    /// 那是 BaitInfo 資產上的 List，改下去整個 session 都會髒掉。
    /// </summary>
    internal static class WeightTable
    {
        private static FieldInfo _fFishable, _fWeight;
        private static bool _resolved;

        // 個別倍率的快取，設定字串沒變就不重新解析
        private static string _parsedFrom;
        private static Dictionary<string, float> _perItem = new Dictionary<string, float>();

        // 保底：上一次抽取時哪些項目算稀有，交給 postfix 判斷有沒有中
        internal static readonly HashSet<Fishable> LastRareSet = new HashSet<Fishable>();
        internal static int MissStreak;

        /// <summary>
        /// 上一次 <see cref="Build"/> 有沒有真的算出稀有名單。postfix 靠它決定要不要
        /// 記保底——沒算出來就沒有稀有度可判，硬記只會用到過期的名單。
        /// </summary>
        internal static bool LastBuildValid;

        internal static bool Ready
        {
            get
            {
                Resolve();
                return _fFishable != null && _fWeight != null;
            }
        }

        /// <summary>回傳調整後的新表；沒有任何調整或資料不合理時回傳 null（代表照原樣跑）。</summary>
        internal static List<ItemInfoWeight> Build(List<ItemInfoWeight> original)
        {
            // **第一件事就是作廢上一輪的稀有名單。** 下面三條 early return 走在
            // LastRareSet.Clear() 前面，走到時 prefix 不換表，但 postfix 仍然會拿
            // __result 去比對**上一次**留下的名單——稀有判定錯，保底計數跟著歪。
            LastBuildValid = false;

            if (original == null || original.Count == 0) return null;
            if (!Ready) return null;

            ParsePerItem();

            float maxWeight = 0f;
            for (int i = 0; i < original.Count; i++)
                if (original[i] != null && original[i].Weight > maxWeight) maxWeight = original[i].Weight;
            if (maxWeight <= 0f) return null;

            float rareCut = maxWeight * Plugin.RareThreshold.Value;

            LastRareSet.Clear();
            var result = new List<ItemInfoWeight>(original.Count);
            var rareOnly = new List<ItemInfoWeight>();
            float total = 0f, rareTotal = 0f;

            for (int i = 0; i < original.Count; i++)
            {
                ItemInfoWeight src = original[i];
                if (src == null || src.Fishable == null) continue;

                bool isBoss = IsBoss(src.Fishable);
                bool isRare = isBoss || src.Weight <= rareCut;
                if (isRare) LastRareSet.Add(src.Fishable);

                float mul;
                float perItem;
                if (_perItem.TryGetValue(KeyOf(src.Fishable), out perItem)) mul = perItem;
                else if (isBoss) mul = Plugin.BossMultiplier.Value;
                else if (isRare) mul = Plugin.RareMultiplier.Value;
                else mul = Plugin.CommonMultiplier.Value;

                float w = Mathf.Max(0f, src.Weight * mul);
                ItemInfoWeight entry = Make(src.Fishable, w);
                result.Add(entry);
                total += w;
                if (isRare) { rareOnly.Add(entry); rareTotal += w; }
            }

            LastBuildValid = true;

            // 保底：連續槓龜夠多次就只留稀有項
            int pity = Plugin.PityAfter.Value;
            if (pity > 0 && MissStreak >= pity && rareOnly.Count > 0 && rareTotal > 0f)
            {
                if (Plugin.LogRolls.Value)
                    Plugin.Log.LogInfo("保底觸發（連續 " + MissStreak + " 次非稀有），本次只抽稀有項。");
                return rareOnly;
            }

            // 全部被歸零就沒得抽了，退回原表比較安全
            if (total <= 0f || result.Count == 0) return null;
            return result;
        }

        internal static bool IsBoss(Fishable f)
        {
            if (f == null || !f.ItemToSpawn) return false;
            Creature c = f.ItemToSpawn as Creature;
            return c && c.BossType > BossType.None;
        }

        /// <summary>跟 GameInfo 建查找表時用的鍵一致：去空格、全小寫。</summary>
        internal static string KeyOf(Fishable f)
        {
            if (f == null || !f.ItemToSpawn) return "";
            return f.ItemToSpawn.name.Replace(" ", "").ToLowerInvariant();
        }

        private static ItemInfoWeight Make(Fishable f, float weight)
        {
            var o = new ItemInfoWeight();
            _fFishable.SetValue(o, f);
            _fWeight.SetValue(o, weight);
            return o;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _fFishable = AccessTools.Field(typeof(ItemInfoWeight), "fishable");
            _fWeight = AccessTools.Field(typeof(ItemInfoWeight), "_weight");

            if (_fFishable == null || _fWeight == null)
                Plugin.Log.LogWarning("ItemInfoWeight 的欄位名對不上（遊戲可能又更新了），權重調整不會生效。");
        }

        private static void ParsePerItem()
        {
            string raw = Plugin.PerItem.Value ?? "";
            if (raw == _parsedFrom) return;
            _parsedFrom = raw;

            var map = new Dictionary<string, float>();
            string[] parts = raw.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] kv = parts[i].Split('=');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().Replace(" ", "").ToLowerInvariant();
                float val;
                if (key.Length == 0) continue;
                if (!float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                {
                    Plugin.Log.LogWarning("個別倍率解析不了：" + parts[i].Trim() + "（小數點請用 .）");
                    continue;
                }
                map[key] = Mathf.Max(0f, val);
            }

            _perItem = map;
            if (map.Count > 0) Plugin.Log.LogInfo("個別倍率已載入 " + map.Count + " 項。");
        }
    }
}

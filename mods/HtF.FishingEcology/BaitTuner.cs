using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HtF.FishingEcology
{
    /// <summary>
    /// 咬鉤時間。
    ///
    /// Bait 是這樣算的（Bait.cs）：
    ///     RandomizedCatchTime = Random.Range(Info.CatchTimeMinMax.x, Info.CatchTimeMinMax.y);
    ///
    /// 沒有去 patch RandomizedCatchTime 的 setter——那是個一行的 auto-property，
    /// Mono 會把它 inline 掉，patch 會靜靜地不生效。
    /// 改成直接調整 BaitInfo 資產上的 _catchTimeMinMax，
    /// 但**先把原值快照起來**，倍率一律從快照算，而且退出時還原，
    /// 免得改過的值在同一個 session 裡髒到別的存檔。
    /// </summary>
    internal static class BaitTuner
    {
        private static FieldInfo _field;
        private static bool _resolved;
        private static bool _applied;
        private static readonly Dictionary<BaitInfo, Vector2> Originals = new Dictionary<BaitInfo, Vector2>();

        /// <summary>每幀輕量檢查：魚餌表載入後套用一次。</summary>
        internal static void TickTryApply()
        {
            if (_applied) return;
            try
            {
                if (GameInfo.AllBaits == null || GameInfo.AllBaits.Count == 0) return;
            }
            catch (Exception) { return; }

            Apply();
        }

        internal static void Apply()
        {
            Resolve();
            if (_field == null) return;

            IReadOnlyList<BaitInfo> baits;
            try
            {
                baits = GameInfo.AllBaits;
                if (baits == null || baits.Count == 0) return;
            }
            catch (Exception) { return; }

            float mul = Plugin.CatchTimeMultiplier.Value;

            for (int i = 0; i < baits.Count; i++)
            {
                BaitInfo b = baits[i];
                if (!b) continue;

                Vector2 original;
                if (!Originals.TryGetValue(b, out original))
                {
                    try { original = (Vector2)_field.GetValue(b); }
                    catch (Exception) { continue; }
                    Originals[b] = original;
                }

                try { _field.SetValue(b, original * mul); }
                catch (Exception) { }
            }

            _applied = true;
            if (Math.Abs(mul - 1f) > 0.0001f)
                Plugin.Log.LogInfo("咬鉤時間 ×" + mul + " 已套用到 " + Originals.Count + " 種魚餌。");
        }

        /// <summary>把資產改回原值。資產是共用的，離開時不還原會髒到同一個 session 的其他存檔。</summary>
        internal static void Restore()
        {
            if (_field == null || Originals.Count == 0) return;
            foreach (KeyValuePair<BaitInfo, Vector2> kv in Originals)
            {
                if (!kv.Key) continue;
                try { _field.SetValue(kv.Key, kv.Value); } catch (Exception) { }
            }
            Originals.Clear();
            _applied = false;
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _field = AccessTools.Field(typeof(BaitInfo), "_catchTimeMinMax");
            if (_field == null)
                Plugin.Log.LogWarning("找不到 BaitInfo._catchTimeMinMax（遊戲可能又更新了），咬鉤時間不會生效。");
        }
    }
}

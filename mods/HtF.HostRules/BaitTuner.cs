using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HtF.HostRules
{
    /// <summary>
    /// 咬鉤時間。
    ///
    /// Bait 是這樣算的（Bait.cs）：
    ///     RandomizedCatchTime = Random.Range(Info.CatchTimeMinMax.x, Info.CatchTimeMinMax.y);
    ///
    /// **這是房主專屬的。** 唯一讀 RandomizedCatchTime 的地方是
    /// CreatureManager.FindFishForBait（CreatureManager.cs:111），只從 TickUpdate 進得去，
    /// 而 TickUpdate 只在 CreatureManager.OnStartServer 掛上 TimeManager.OnPostTick——
    /// 純客戶端調了不會有任何效果，房主調了就是整房都變。
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

        /// <summary>
        /// 每幀輕量檢查：魚餌表載入後套用一次。
        ///
        /// 用 <see cref="_applied"/> 當閘門是安全的：<c>GameInfo._allBaits</c> 是
        /// <c>GameInfo.Awake</c> 用 <c>Resources.LoadAll&lt;BaitInfo&gt;("Baits")</c> 填一次的
        /// static 清單，從來不 clear，裡面是整個行程共用的同一批 ScriptableObject 資產。
        /// 回主選單、換存檔都不會產生新的 BaitInfo 實例，所以套過一次就不需要再套。
        /// </summary>
        internal static void TickTryApply()
        {
            if (_applied || !Enabled) return;
            try
            {
                if (GameInfo.AllBaits == null || GameInfo.AllBaits.Count == 0) return;
            }
            catch (Exception) { return; }

            Apply();
        }

        /// <summary>「啟用釣魚生態」關掉、或偵測到舊 mod 衝突時，這一半也要停。</summary>
        private static bool Enabled
        {
            get
            {
                return !Plugin.FishingDisabledByConflict
                       && Plugin.FishingEnabled != null && Plugin.FishingEnabled.Value;
            }
        }

        internal static void Apply()
        {
            Resolve();
            if (_field == null) return;

            // 關掉「啟用釣魚生態」時要**還原**，不是放著不管。
            // 少了這一行，關掉之後 BaitInfo 上被寫進去的倍率會留著，
            // 而且 Config.SettingChanged 會在關掉的當下再套一次——
            // 使用者以為退回原生了，魚其實照樣咬得比原生快。
            if (!Enabled)
            {
                Restore();
                return;
            }

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

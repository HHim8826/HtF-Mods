using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HtF.HostRules
{
    /// <summary>
    /// 調整 PlayerVitals 上那些 [SerializeField] 的私有數值。
    ///
    /// 這些欄位驅動的 tick（飽食、回血、中毒、著火）全部只在伺服器端跑
    /// ——OnStartServer 才會把 TickUpdate 掛上 TimeManager.OnTick——
    /// 所以在房主端改就夠了，不需要客戶端配合。
    ///
    /// 倍率一律作用在「prefab 原始值」上，不是在當前值上疊加，
    /// 所以反覆套用不會愈滾愈大。
    /// </summary>
    internal static class VitalsTuner
    {
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>();
        private static bool _defaultsCaptured;

        // 名稱、型別都對照 decompiled-current/PlayerVitals.cs
        private const string LoseFullnessInterval = "_loseFullnessTickInterval";      // uint 300
        private const string FullnessLostPerTick = "_fullnessLostPerTickInterval";    // uint 1
        private const string LoseHealthHungerInterval = "_loseHealthHungerTickInterval"; // uint 150
        private const string HealthLostPerHungerTick = "_healthLostPerHungerTick";    // int 5
        private const string GainHealthInterval = "_gainHealthTickInterval";          // uint 100
        private const string HealthGainedPerTick = "_healthGainedPerTickInterval";    // int 5
        private const string PoisonInterval = "_poisonTickInterval";                  // uint 100
        private const string PoisonDamage = "_poisonDamagePerTickInterval";           // int 5
        private const string FireInterval = "_fireTickInterval";                      // uint 50
        private const string FireDamage = "_fireDamagePerTickInterval";               // int 10
        private const string PvpDamage = "_playerDamageMultiplier";                   // float 0.25
        private const string HealthOnRes = "_healthOnRes";                            // int 25
        private const string FullnessOnRes = "_fullnessOnRes";                        // int 10
        private const string InvulnAfterDamage = "_invulnerabilityAfterDamage";       // float 0.25

        private static readonly string[] All =
        {
            LoseFullnessInterval, FullnessLostPerTick, LoseHealthHungerInterval, HealthLostPerHungerTick,
            GainHealthInterval, HealthGainedPerTick, PoisonInterval, PoisonDamage,
            FireInterval, FireDamage, PvpDamage, HealthOnRes, FullnessOnRes, InvulnAfterDamage,
        };

        internal static void Apply(PlayerVitals v)
        {
            if (!v) return;
            CaptureDefaults(v);

            // 速度倍率：間隔越小越快，所以除
            SetUInt(v, LoseFullnessInterval, DivInterval(LoseFullnessInterval, Plugin.HungerSpeedMul.Value));
            SetUInt(v, LoseHealthHungerInterval, DivInterval(LoseHealthHungerInterval, Plugin.HungerSpeedMul.Value));
            SetInt(v, HealthLostPerHungerTick, MulInt(HealthLostPerHungerTick, Plugin.HungerDamageMul.Value));

            SetUInt(v, GainHealthInterval, DivInterval(GainHealthInterval, Plugin.RegenSpeedMul.Value));
            SetInt(v, HealthGainedPerTick, MulInt(HealthGainedPerTick, Plugin.RegenAmountMul.Value));

            SetInt(v, PoisonDamage, MulInt(PoisonDamage, Plugin.PoisonDamageMul.Value));
            SetInt(v, FireDamage, MulInt(FireDamage, Plugin.FireDamageMul.Value));

            if (Plugin.PvpDamageMul.Value >= 0f) SetFloat(v, PvpDamage, Plugin.PvpDamageMul.Value);
            else RestoreFloat(v, PvpDamage);

            if (Plugin.HealthOnRes.Value >= 0) SetInt(v, HealthOnRes, Plugin.HealthOnRes.Value);
            else RestoreInt(v, HealthOnRes);

            if (Plugin.FullnessOnRes.Value >= 0) SetInt(v, FullnessOnRes, Plugin.FullnessOnRes.Value);
            else RestoreInt(v, FullnessOnRes);

            if (Plugin.InvulnAfterDamage.Value >= 0f) SetFloat(v, InvulnAfterDamage, Plugin.InvulnAfterDamage.Value);
            else RestoreFloat(v, InvulnAfterDamage);
        }

        /// <summary>設定改動後重新套用到場上每一位玩家。</summary>
        internal static void ReapplyAll()
        {
            try
            {
                if (PlayerManager.Players == null) return;
                for (int i = 0; i < PlayerManager.Players.Count; i++)
                {
                    Player p = PlayerManager.Players[i];
                    if (p && p.Vitals) Apply(p.Vitals);
                }
            }
            catch (Exception e) { Plugin.Log.LogError("重新套用失敗：" + e); }
        }

        // ------------------------------------------------------------------ 反射

        private static void CaptureDefaults(PlayerVitals v)
        {
            if (_defaultsCaptured) return;
            _defaultsCaptured = true;

            for (int i = 0; i < All.Length; i++)
            {
                FieldInfo f = AccessTools.Field(typeof(PlayerVitals), All[i]);
                if (f == null)
                {
                    Plugin.Log.LogWarning("PlayerVitals 找不到欄位 " + All[i] + "（遊戲可能又更新了），該項會被略過。");
                    continue;
                }
                Fields[All[i]] = f;
                try { Defaults[All[i]] = f.GetValue(v); }
                catch (Exception e) { Plugin.Log.LogWarning("讀取 " + All[i] + " 失敗：" + e.Message); }
            }
            Plugin.Log.LogInfo("已擷取 PlayerVitals 預設值 " + Defaults.Count + " 項。");
        }

        private static uint DivInterval(string key, float speedMul)
        {
            object d;
            if (!Defaults.TryGetValue(key, out d)) return 0u;
            float baseVal = Convert.ToSingle(d);
            if (speedMul <= 0.0001f) speedMul = 0.0001f;
            return (uint)Mathf.Max(1f, Mathf.Round(baseVal / speedMul));
        }

        private static int MulInt(string key, float mul)
        {
            object d;
            if (!Defaults.TryGetValue(key, out d)) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(Convert.ToSingle(d) * mul));
        }

        private static void SetUInt(PlayerVitals v, string key, uint value)
        {
            FieldInfo f;
            if (!Fields.TryGetValue(key, out f) || !Defaults.ContainsKey(key)) return;
            try { f.SetValue(v, value); } catch (Exception) { }
        }

        private static void SetInt(PlayerVitals v, string key, int value)
        {
            FieldInfo f;
            if (!Fields.TryGetValue(key, out f) || !Defaults.ContainsKey(key)) return;
            try { f.SetValue(v, value); } catch (Exception) { }
        }

        private static void SetFloat(PlayerVitals v, string key, float value)
        {
            FieldInfo f;
            if (!Fields.TryGetValue(key, out f) || !Defaults.ContainsKey(key)) return;
            try { f.SetValue(v, value); } catch (Exception) { }
        }

        private static void RestoreInt(PlayerVitals v, string key) { Restore(v, key); }
        private static void RestoreFloat(PlayerVitals v, string key) { Restore(v, key); }

        private static void Restore(PlayerVitals v, string key)
        {
            FieldInfo f;
            object d;
            if (!Fields.TryGetValue(key, out f) || !Defaults.TryGetValue(key, out d)) return;
            try { f.SetValue(v, d); } catch (Exception) { }
        }
    }
}

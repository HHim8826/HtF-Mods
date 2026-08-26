using System;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Managing.Timing;
using HarmonyLib;
using UnityEngine;

namespace HtF.RadioMusic
{
    // 類別上這個空的 [HarmonyPatch] 是必要的：PatchClassProcessor 若在型別上
    // 找不到任何 Harmony 標註就直接略過整個類別，方法上的標註不會被掃到。
    [HarmonyPatch]
    internal static class RadioPatcher
    {
        internal const string ApplyVolumeName = "ApplyVolume";
        internal const string OnBossDeathName = "OnBossDeath";

        private static MethodInfo _mApplyVolume;
        private static FieldInfo _fChannels, _fNoise, _fRadioVol, _fLocalFreq, _fChannelSource;
        private static FieldInfo _fStick, _fFreqMinMax;
        private static bool _resolved;

        // 場上活著的收音機，音樂載完後要回頭套用
        private static readonly List<Radio> Live = new List<Radio>();

        [HarmonyPatch(typeof(Radio), nameof(Radio.OnStartClient))]
        [HarmonyPostfix]
        private static void Radio_OnStartClient_Postfix(Radio __instance)
        {
            if (!Live.Contains(__instance)) Live.Add(__instance);
            ApplyClips(__instance);
        }

        /// <summary>
        /// **Boss 被打死之後把音量算回來。**
        ///
        /// 這是遊戲自己的 bug，而且兩條收尾路徑不一致：
        ///
        /// ```csharp
        /// // BossManager.OnBossDeath（BossManager.cs:239-252）
        /// onGlobalBossDeath();          // ← 事件先發
        /// ...
        /// BossManager.Boss = null;      // ← Boss 之後才清掉
        ///
        /// // BossManager.OnBossDespawn（BossManager.cs:300-301）
        /// BossManager.Boss = null;      // ← 這條是先清
        /// onGlobalBossDespawn();
        /// ```
        ///
        /// `Radio` 把 `ApplyVolume` 掛在 `OnGlobalBossDeath` / `OnGlobalBossDespawn`
        /// 上（`Radio.cs:240-242`），而 `ApplyVolume` 開頭是
        /// `if (BossManager.Boss) { 全部靜音; return; }`。死亡那條路徑觸發時
        /// `Boss` 還在，所以它**又靜音一次就 return**——而且之後沒有任何東西
        /// 會再呼叫 `ApplyVolume`，收音機就永遠不會再響。
        /// 消失（despawn）那條因為先清 `Boss`，反而是正常的。
        ///
        /// 修法：掛在 `OnBossDeath` 的 postfix。方法回傳時 `Boss` 已經是 null 了，
        /// 這時候再呼叫一次遊戲自己的 `ApplyVolume` 就好——那正是 despawn 路徑
        /// 做的事，所以行為完全一致，不需要自己重算。
        /// </summary>
        [HarmonyPatch(typeof(BossManager), OnBossDeathName)]
        [HarmonyPostfix]
        private static void BossManager_OnBossDeath_Postfix()
        {
            // 早退的情況（_instance 或 Boss 是 null）postfix 一樣會跑，
            // 但那時候本來就沒有靜音發生，再算一次也只是把同樣的值寫回去。
            RefreshAll();
        }

        /// <summary>對場上每一台收音機重新套用一次遊戲自己的音量計算。</summary>
        private static void RefreshAll()
        {
            if (_mApplyVolume == null) _mApplyVolume = AccessTools.Method(typeof(Radio), ApplyVolumeName);
            if (_mApplyVolume == null) return;

            Live.RemoveAll(r => !r);
            for (int i = 0; i < Live.Count; i++)
            {
                try { _mApplyVolume.Invoke(Live[i], null); }
                catch (Exception e) { Plugin.Log.LogWarning("Boss 結束後重算收音機音量失敗：" + e.Message); }
            }
        }

        /// <summary>
        /// 沙沙聲與音量。
        ///
        /// 原本最後一行是：_noiseSource.volume = (1f - 最接近頻道的準度) * 0.075f;
        /// 那是刻意模擬 FM 空頻雜訊——調準了是 0，偏掉才變大。這裡讓它可調（0 = 關掉）。
        /// ApplyVolume 是私有方法，但夠大不會被 Mono inline，patch 是可靠的。
        /// </summary>
        [HarmonyPatch(typeof(Radio), ApplyVolumeName)]
        [HarmonyPostfix]
        private static void Radio_ApplyVolume_Postfix(Radio __instance)
        {
            Resolve();
            if (_fNoise == null) return;

            try
            {
                bool bossSilenced = BossManager.Boss;
                if (bossSilenced && Plugin.PlayThroughBoss.Value)
                {
                    Recompute(__instance);
                    return;
                }

                var noise = _fNoise.GetValue(__instance) as AudioSource;
                if (noise) noise.volume *= Plugin.NoiseVolume.Value;

                if (!bossSilenced && Math.Abs(Plugin.MusicVolume.Value - 1f) > 0.0001f)
                {
                    RadioChannel[] channels = Channels(__instance);
                    if (channels == null) return;
                    for (int i = 0; i < channels.Length; i++)
                    {
                        AudioSource src = SourceOf(channels[i]);
                        if (src) src.volume *= Plugin.MusicVolume.Value;
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("套用音量失敗：" + e.Message); }
        }

        /// <summary>
        /// Boss 出現時遊戲會把所有頻道和雜訊都設成 0 並提早 return。
        /// 想繼續聽音樂就得自己把原本那段算一遍。
        /// </summary>
        private static void Recompute(Radio radio)
        {
            RadioChannel[] channels = Channels(radio);
            if (channels == null || _fLocalFreq == null || _fRadioVol == null) return;

            // 頻率來源要跟遊戲同一條判斷（Radio.cs:121）：
            //     (Holder && Holder.Owner.IsLocalClient) ? _localFrequency : _frequency.Value
            //
            // 之前這裡無條件讀 _localFrequency，而那個欄位**不是**隨時都追得上：
            // OnFrequencyChange（Radio.cs:189-201）是先 ApplyVolume()、之後才寫
            // _localFrequency = next，我們的 postfix 就掛在那次 ApplyVolume 後面，
            // 讀到的必然是上一次的頻率。結果是別人在轉台時，Boss 期間我們會用舊頻率
            // 算音量——放錯頻道，或某個頻道該解除靜音卻沒有。
            float freq = (radio.Holder && radio.Holder.Owner != null && radio.Holder.Owner.IsLocalClient)
                ? (float)_fLocalFreq.GetValue(radio)
                : radio._frequency.Value;
            float radioVol = (float)_fRadioVol.GetValue(radio);
            float tickTime = 0f;
            try { tickTime = (float)radio.TimeManager.TicksToTime(TickType.Tick); }
            catch (Exception) { }

            float best = 0f;
            for (int i = 0; i < channels.Length; i++)
            {
                RadioChannel ch = channels[i];
                if (ch == null) continue;

                float d = Mathf.Abs(freq - ch.Frequency) - 0.5f;
                d = Mathf.Clamp01(1f - d);
                float vol = d * radioVol * Plugin.MusicVolume.Value;

                if (ch.IsMuted && vol > 0.05f) ch.ToggleMute(false, tickTime);
                else if (!ch.IsMuted && vol < 0.05f) ch.ToggleMute(true, 0f);

                ch.SetVol(vol);
                if (d > best) best = d;
            }

            var noise = _fNoise.GetValue(radio) as AudioSource;
            if (noise) noise.volume = (1f - best) * 0.075f * Plugin.NoiseVolume.Value;

            MoveFrequencyStick(radio, freq);
        }

        /// <summary>
        /// ApplyVolume 最後還會把指針推到對應位置（Radio.cs:154）。我們在 Boss 期間
        /// 等於接手了整個 ApplyVolume，不補這一段的話轉台時指針會卡住不動。
        /// 純視覺，兩個欄位任一個抓不到就安靜跳過。
        /// </summary>
        private static void MoveFrequencyStick(Radio radio, float freq)
        {
            if (_fStick == null || _fFreqMinMax == null) return;
            try
            {
                var stick = _fStick.GetValue(radio) as Transform;
                if (!stick) return;
                Vector2 range = (Vector2)_fFreqMinMax.GetValue(radio);
                stick.localPosition = -Vector3.right * (Mathf.InverseLerp(range.x, range.y, freq) * 0.3f);
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ 換曲

        internal static void ApplyToAll()
        {
            Live.RemoveAll(r => !r);
            for (int i = 0; i < Live.Count; i++) ApplyClips(Live[i]);
        }

        /// <summary>
        /// 這個 clip 是否還掛在場上某台收音機的 AudioSource 上。
        /// MusicLibrary 用它決定舊 clip 現在能不能安全銷毀。
        /// </summary>
        internal static bool IsClipInUse(AudioClip clip)
        {
            if (!clip) return false;
            for (int i = 0; i < Live.Count; i++)
            {
                Radio radio = Live[i];
                if (!radio) continue;

                RadioChannel[] channels = Channels(radio);
                if (channels == null) continue;
                for (int c = 0; c < channels.Length; c++)
                {
                    AudioSource src = SourceOf(channels[c]);
                    if (src && src.clip == clip) return true;
                }
            }
            return false;
        }

        private static void ApplyClips(Radio radio)
        {
            if (!radio || !MusicLibrary.Ready || MusicLibrary.TotalClips == 0) return;
            Resolve();

            RadioChannel[] channels = Channels(radio);
            if (channels == null) return;

            for (int i = 0; i < channels.Length; i++)
            {
                AudioClip clip = MusicLibrary.ClipFor(i, channels.Length);
                if (!clip) continue; // 沒有對應曲目就保留原曲

                AudioSource src = SourceOf(channels[i]);
                if (!src || src.clip == clip) continue;

                bool wasPlaying = src.isPlaying;
                src.clip = clip;
                // ToggleMute 解除靜音時會做 _channelSource.time = t % clip.length，
                // clip 為 null 會 NRE，所以上面確定 clip 不是 null 才換。
                if (wasPlaying)
                {
                    src.time = 0f;
                    src.Play();
                }
            }
        }

        // ------------------------------------------------------------------ 反射

        private static RadioChannel[] Channels(Radio radio)
        {
            Resolve();
            if (_fChannels == null) return null;
            try { return _fChannels.GetValue(radio) as RadioChannel[]; }
            catch (Exception) { return null; }
        }

        private static AudioSource SourceOf(RadioChannel channel)
        {
            Resolve();
            if (_fChannelSource == null || channel == null) return null;
            try { return _fChannelSource.GetValue(channel) as AudioSource; }
            catch (Exception) { return null; }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _fChannels = AccessTools.Field(typeof(Radio), "_channels");
            _fNoise = AccessTools.Field(typeof(Radio), "_noiseSource");
            _fRadioVol = AccessTools.Field(typeof(Radio), "_radioVol");
            _fLocalFreq = AccessTools.Field(typeof(Radio), "_localFrequency");
            _fChannelSource = AccessTools.Field(typeof(RadioChannel), "_channelSource");
            _fStick = AccessTools.Field(typeof(Radio), "_frequencyStick");
            _fFreqMinMax = AccessTools.Field(typeof(Radio), "FreqMinMax");

            if (_fChannels == null || _fChannelSource == null)
                Plugin.Log.LogError("Radio 的欄位名對不上（遊戲可能又更新了），換曲不會生效。");
        }

        /// <summary>啟動時確認字串指定的目標還在，遊戲更新後改名才不會默默失效。</summary>
        internal static void VerifyTargets()
        {
            if (AccessTools.Method(typeof(Radio), ApplyVolumeName) == null)
                Plugin.Log.LogWarning("Radio." + ApplyVolumeName + " 不存在了（遊戲更新？）——雜訊與音量設定不會生效。");

            if (AccessTools.Method(typeof(BossManager), OnBossDeathName) == null)
                Plugin.Log.LogWarning("BossManager." + OnBossDeathName
                    + " 不存在了（遊戲更新？）——Boss 被打死之後收音機不會自己恢復。");
        }
    }
}

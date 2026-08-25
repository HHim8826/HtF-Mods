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

        private static FieldInfo _fChannels, _fNoise, _fRadioVol, _fLocalFreq, _fChannelSource;
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

            // _localFrequency 在所有情況下都追得上目前頻率：
            // 本機持有時自己更新，別人持有時由 OnFrequencyChange 寫入。
            float freq = (float)_fLocalFreq.GetValue(radio);
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
        }

        // ------------------------------------------------------------------ 換曲

        internal static void ApplyToAll()
        {
            Live.RemoveAll(r => !r);
            for (int i = 0; i < Live.Count; i++) ApplyClips(Live[i]);
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

            if (_fChannels == null || _fChannelSource == null)
                Plugin.Log.LogError("Radio 的欄位名對不上（遊戲可能又更新了），換曲不會生效。");
        }

        /// <summary>啟動時確認字串指定的目標還在，遊戲更新後改名才不會默默失效。</summary>
        internal static void VerifyTargets()
        {
            if (AccessTools.Method(typeof(Radio), ApplyVolumeName) == null)
                Plugin.Log.LogWarning("Radio." + ApplyVolumeName + " 不存在了（遊戲更新？）——雜訊與音量設定不會生效。");
        }
    }
}

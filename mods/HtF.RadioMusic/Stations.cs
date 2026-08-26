using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HtF.RadioMusic
{
    /// <summary>
    /// 把電台的頻道數改掉，讓曲目**一首一個頻率**平均分佈在整條波段上。
    ///
    /// 遊戲原本的頻道是 `Radio._channels`（`RadioChannel[]`，序列化在 prefab 上），
    /// 數量固定。歌比頻道多的時候，多出來的只能靠節目表接著播——同一個頻率上會
    /// 輪流出現不同的歌。想要「轉到 91.0 就是這首、轉到 94.0 就是那首」，
    /// 就得真的把頻道加出來。
    ///
    /// **這件事做得到，因為 `_channels` 沒有任何地方寫死索引或數量**（已查證：
    /// `Radio.cs` 只有四處碰它，`ToggleChannels`、`ApplyVolume` 兩處迴圈、
    /// 欄位宣告本身，全都是逐一走訪）。所以換成更長的陣列是安全的。
    ///
    /// 三件實作上必須小心的事：
    ///
    /// 1. **新頻道的 `AudioSource` 一定要有 clip。** `RadioChannel.ToggleMute(false, t)`
    ///    的第一行是 `_channelSource.time = t % _channelSource.clip.length;`——
    ///    clip 是 null 就 NRE，而 `Radio.OnStartClient` 和 `OnEnable` 都會呼叫
    ///    `ToggleChannels(true)` 把每個頻道解除靜音。所以新的 source 是**複製**
    ///    現有那個 GameObject 來的：連 clip、3D 衰減、mixer group 一起帶過來，
    ///    位置也對得上（收音機是位置音源，差一點就會聽起來在別的地方）。
    /// 2. **頻道之間要夠遠。** `ApplyVolume` 算的是
    ///    `Clamp01(1 - (|目前頻率 - 頻道頻率| - 0.5))`——誤差 0.5 以內滿音量，
    ///    到 1.5 才完全消失。所以兩台相距 3.0 才聽得乾淨；更密就會互相滲音。
    ///    88–108 共 20，最多放得下 7 台。
    /// 3. **這是本機的東西。** 頻道是我們自己在本機生的，別人沒裝（或設定不同）
    ///    就對不上——同步的只有「你轉到幾點幾」這個數字本身。
    /// </summary>
    internal static class Stations
    {
        /// <summary>兩台之間要多遠才不互相滲音。出自 ApplyVolume 的 ±1.5 衰減。</summary>
        internal const float CleanSpacing = 3.0f;

        private static FieldInfo _fFrequency;
        private static bool _resolved;

        /// <summary>我們替每台收音機生出來的 AudioSource，重建時要自己銷毀。</summary>
        private static readonly Dictionary<Radio, List<AudioSource>> Created =
            new Dictionary<Radio, List<AudioSource>>();

        // ------------------------------------------------------------------ 對外

        /// <summary>
        /// 依設定重建這台收音機的頻道。設定是「不改」或算出來的數量沒變就什麼都不做。
        /// </summary>
        internal static void Rebuild(Radio radio, RadioChannel[] current, int trackCount)
        {
            if (!radio || current == null || current.Length == 0) return;
            Resolve();
            if (_fFrequency == null) return;

            int want = Desired(current.Length, trackCount);
            if (want <= 0) return;                 // −1 = 不改，或自動模式還沒有曲目

            Vector2 band = Band(radio);
            try
            {
                // 數量剛好一樣時 Resize 會回 null，但頻率還是要重攤一次——
                // 使用者明講了要幾個頻道，就該是平均分佈的，不能留著原本不規則的位置。
                RadioChannel[] rebuilt = Resize(radio, current, want) ?? current;

                Spread(rebuilt, band);
                RadioPatcher.SetChannels(radio, rebuilt);
                Announce(rebuilt.Length, band);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("重建收音機頻道失敗，維持原本的頻道：" + e.Message);
            }
        }

        /// <summary>
        /// 收音機不見了就把紀錄清掉。clone 是它的子物件，會跟著一起被銷毀，
        /// 這裡清的只是字典裡那筆（key 會變成 Unity 的假 null，留著是純浪費）。
        /// </summary>
        internal static void PruneDead()
        {
            List<Radio> dead = null;
            foreach (var kv in Created)
                if (!kv.Key) (dead ?? (dead = new List<Radio>())).Add(kv.Key);
            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++) Created.Remove(dead[i]);
        }

        // ------------------------------------------------------------------ 數量

        /// <summary>
        /// 要幾個頻道。
        /// −1 = 不改；0 = 自動（跟著曲目數，但不超過波段放得下的數量）；其餘 = 照設定。
        /// </summary>
        private static int Desired(int vanilla, int trackCount)
        {
            int cfg = Plugin.ChannelCount != null ? Plugin.ChannelCount.Value : -1;
            if (cfg < 0) return 0;          // 不改
            if (cfg > 0) return cfg;        // 使用者指定，即使會滲音也照做

            if (trackCount <= 0) return 0;  // 沒有自訂音樂就沒必要動
            return Mathf.Clamp(trackCount, vanilla, MaxClean());
        }

        /// <summary>波段裡放得下幾個「不互相滲音」的頻道。</summary>
        private static int MaxClean()
        {
            float spacing = Plugin.MinSpacing != null ? Plugin.MinSpacing.Value : CleanSpacing;
            if (spacing <= 0.01f) return 20;
            return Mathf.Max(1, Mathf.FloorToInt(20f / spacing) + 1);
        }

        // ------------------------------------------------------------------ 建與改

        /// <summary>把陣列調整成指定長度：不夠就複製第一個頻道的 source 生新的，多了就砍掉。</summary>
        private static RadioChannel[] Resize(Radio radio, RadioChannel[] current, int want)
        {
            var list = new List<RadioChannel>(current);

            List<AudioSource> mine;
            if (!Created.TryGetValue(radio, out mine)) Created[radio] = mine = new List<AudioSource>();

            // 縮小：先砍我們自己生的，遊戲原本的頻道一個都不動。
            while (list.Count > want && mine.Count > 0)
            {
                AudioSource doomed = mine[mine.Count - 1];
                mine.RemoveAt(mine.Count - 1);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (RadioPatcher.SourceOf(list[i]) != doomed) continue;
                    list.RemoveAt(i);
                    break;
                }
                if (doomed) Object.Destroy(doomed.gameObject);
            }

            // 放大：複製第一個頻道的 GameObject。
            // 連 clip 一起帶過來是刻意的——ToggleMute 會解參照 clip.length，null 會 NRE。
            AudioSource template = RadioPatcher.SourceOf(current[0]);
            if (!template) return list.Count == current.Length ? null : list.ToArray();

            while (list.Count < want)
            {
                AudioSource clone = Clone(template);
                if (!clone) break;

                var channel = new RadioChannel();
                RadioPatcher.SetSource(channel, clone);
                list.Add(channel);
                mine.Add(clone);
            }

            return list.Count == current.Length ? null : list.ToArray();
        }

        /// <summary>
        /// 生一個和 <paramref name="template"/> 設定相同的 AudioSource。
        ///
        /// **不能無條件 `Instantiate` 它的 GameObject。** 頻道的 AudioSource 掛在哪裡是
        /// prefab 決定的，反編譯看不到——如果它就掛在收音機自己那個物件上
        /// （同一個 GameObject 掛好幾個 AudioSource 是合法的），複製它等於複製整台收音機。
        /// 所以先確認那個物件的子樹裡沒有 <c>Radio</c>：
        ///
        /// - 沒有（獨立的子物件）→ 直接 `Instantiate`，設定一個都不會漏，包含自訂衰減曲線。
        /// - 有（跟收音機同體）→ 退而求其次：開一個空的子物件、`AddComponent`，
        ///   再逐項抄設定。會走到這條路時留一行 log，出問題才查得到。
        /// </summary>
        private static AudioSource Clone(AudioSource template)
        {
            GameObject host = template.gameObject;

            if (!host.GetComponentInChildren<Radio>(true))
            {
                GameObject go = Object.Instantiate(host, host.transform.parent);
                // Instantiate(原件, parent) 保的是世界座標，這裡要的是「跟原本那個重疊」。
                go.transform.localPosition = host.transform.localPosition;
                go.transform.localRotation = host.transform.localRotation;
                go.transform.localScale = host.transform.localScale;
                go.name = host.name + " (HtF)";

                AudioSource cloned = go.GetComponent<AudioSource>();
                if (!cloned) { Object.Destroy(go); return null; }

                // 音量交給 ApplyVolume 決定；一開始先閉嘴，免得建出來那一瞬間全部一起響。
                cloned.volume = 0f;
                return cloned;
            }

            WarnFallback();
            var child = new GameObject("RadioChannel (HtF)");
            child.transform.SetParent(host.transform, false);

            AudioSource src = child.AddComponent<AudioSource>();
            CopySettings(template, src);
            src.volume = 0f;
            return src;
        }

        private static bool _warnedFallback;

        private static void WarnFallback()
        {
            if (_warnedFallback) return;
            _warnedFallback = true;
            Plugin.Log.LogInfo("頻道的 AudioSource 和收音機掛在同一個物件上，"
                + "改用逐項抄設定的方式生新頻道（不能直接複製那個物件，會連整台收音機一起複製）。"
                + "新頻道的音色若和原本不一樣，原因在這裡。");
        }

        /// <summary>
        /// 逐項抄 AudioSource 的設定。只在上面那條退路會用到。
        /// 自訂衰減曲線用 Get/SetCustomCurve 帶過去——那是最容易漏、
        /// 而且漏了會讓聲音距離感整個跑掉的東西。
        /// </summary>
        private static void CopySettings(AudioSource from, AudioSource to)
        {
            to.clip = from.clip;
            to.outputAudioMixerGroup = from.outputAudioMixerGroup;
            to.mute = from.mute;
            to.bypassEffects = from.bypassEffects;
            to.bypassListenerEffects = from.bypassListenerEffects;
            to.bypassReverbZones = from.bypassReverbZones;
            to.playOnAwake = from.playOnAwake;
            to.loop = from.loop;
            to.priority = from.priority;
            to.pitch = from.pitch;
            to.panStereo = from.panStereo;
            to.spatialBlend = from.spatialBlend;
            to.spatialize = from.spatialize;
            to.spatializePostEffects = from.spatializePostEffects;
            to.reverbZoneMix = from.reverbZoneMix;
            to.dopplerLevel = from.dopplerLevel;
            to.spread = from.spread;
            to.rolloffMode = from.rolloffMode;
            to.minDistance = from.minDistance;
            to.maxDistance = from.maxDistance;
            to.ignoreListenerVolume = from.ignoreListenerVolume;
            to.ignoreListenerPause = from.ignoreListenerPause;
            to.velocityUpdateMode = from.velocityUpdateMode;

            try
            {
                to.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                                  from.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
                to.SetCustomCurve(AudioSourceCurveType.SpatialBlend,
                                  from.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
                to.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix,
                                  from.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
                to.SetCustomCurve(AudioSourceCurveType.Spread,
                                  from.GetCustomCurve(AudioSourceCurveType.Spread));
            }
            catch (Exception) { /* 曲線抄不過去不影響能不能出聲 */ }
        }

        /// <summary>頻率平均攤在整條波段上，頭尾各留半格避免貼著邊界。</summary>
        private static void Spread(RadioChannel[] channels, Vector2 band)
        {
            if (channels.Length == 1)
            {
                _fFrequency.SetValue(channels[0], (band.x + band.y) * 0.5f);
                return;
            }

            float step = (band.y - band.x) / (channels.Length - 1);
            for (int i = 0; i < channels.Length; i++)
                _fFrequency.SetValue(channels[i], band.x + step * i);
        }

        private static Vector2 Band(Radio radio)
        {
            Vector2 fallback = new Vector2(88f, 108f);
            try
            {
                Vector2 v = RadioPatcher.FreqRange(radio);
                return (v.y - v.x) > 1f ? v : fallback;
            }
            catch (Exception) { return fallback; }
        }

        private static int _announcedFor = -1;

        private static void Announce(int count, Vector2 band)
        {
            if (_announcedFor == count) return;   // 每台收音機都會走一次，只說一遍
            _announcedFor = count;

            float step = count > 1 ? (band.y - band.x) / (count - 1) : 0f;
            string line = "電台頻道改成 " + count + " 個，"
                          + band.x.ToString("0.#") + "–" + band.y.ToString("0.#")
                          + " 之間每 " + step.ToString("0.##") + " 一台。";

            if (count > 1 && step < CleanSpacing)
                Plugin.Log.LogWarning(line + " 間距小於 " + CleanSpacing
                    + "，相鄰的台會互相滲音（ApplyVolume 的衰減到 ±1.5 才歸零）。"
                    + "要乾淨的話把頻道數調低，或把「最小頻道間距」調小以接受這個結果。");
            else
                Plugin.Log.LogInfo(line);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _fFrequency = AccessTools.Field(typeof(RadioChannel), "_frequency");
            if (_fFrequency == null)
                Plugin.Log.LogWarning("找不到 RadioChannel._frequency（遊戲更新？）——頻道數設定不會生效。");
        }
    }
}

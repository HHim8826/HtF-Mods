using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;

namespace HtF.RadioMusic
{
    /// <summary>
    /// 「大家聽到同一首、同一個位置」。
    ///
    /// **遊戲本來就做了一半。** `RadioChannel.ToggleMute` 解除靜音時是這樣定位的：
    ///
    /// ```csharp
    /// this._channelSource.time = time % this._channelSource.clip.length;
    /// this._channelSource.Play();
    /// ```
    ///
    /// 而呼叫端傳進去的 `time` 是 `TimeManager.TicksToTime(TickType.Tick)`
    /// ——**網路時間**，FishNet 讓每個客戶端的 `Tick` 對齊。所以原版電台本來就是
    /// 同步的：兩個人同時轉到同一台，聽到的是同一首歌的同一個位置。
    ///
    /// 那自訂音樂為什麼會各聽各的？因為缺三塊：
    ///
    /// 1. **檔案要一樣**。這一塊沒有辦法用程式解決——mod 不能替你發音檔。
    ///    每個人的資料夾內容必須相同，這是這個功能唯一的前提。
    /// 2. **選曲要一致**。載入時已經是 `StringComparer.OrdinalIgnoreCase` 排序＋
    ///    輪流分配，所以同樣的檔案在每台機器上會落到同一個頻道；但「同一頻道有
    ///    多首時放第幾首」原本是本機的游標（F7 切換），那個一按就散了。
    ///    這裡改成**從網路時間推出來**，誰都不用送封包。
    /// 3. **換 clip 之後要重新定位**。原本換曲是 `src.time = 0f`，等於從頭播，
    ///    直接掉出共同的時間軸。
    ///
    /// 做法是把一個頻道的曲目串成一條連續的時間軸（就像真的電台節目表）：
    /// 總長 = 各首長度相加，`網路時間 % 總長` 落在哪一首的哪一秒，就播那裡。
    /// 這是純函數——同樣的檔案、同樣的時鐘，每台機器算出來必然一樣，
    /// **不需要新增任何同步狀態**（Harmony 本來也補不上 SyncVar 或 ServerRpc）。
    /// </summary>
    internal static class Sync
    {
        /// <summary>
        /// 網路時間（秒）。拿不到就回負數，呼叫端會跳過同步。
        ///
        /// **不要直接用 <c>InstanceFinder.TimeManager</c>**：它會走
        /// <c>InstanceFinder.NetworkManager</c>，而那個在找不到的時候**每次呼叫**都會
        /// <c>Debug.Log("NetworkManager not found in any open scenes.")</c>
        /// （FishNet 的 `InstanceFinder.cs:40`）——在主選單每幀問一次就是每幀一行 log。
        /// 先看靜態集合 <c>NetworkManager.Instances</c> 就不會踩到。
        /// </summary>
        internal static double Now()
        {
            try
            {
                if (NetworkManager.Instances.Count == 0) return -1.0;
                TimeManager tm = InstanceFinder.TimeManager;
                if (tm == null) return -1.0;
                return tm.TicksToTime(TickType.Tick);
            }
            catch (Exception) { return -1.0; }
        }

        /// <summary>長度小於這個值的 clip 當成無效，避免除以零和無限迴圈。</summary>
        private const float MinLength = 0.05f;

        /// <summary>
        /// 這個頻道**現在**該播哪一首的第幾秒。
        /// 回傳 false = 沒有可用曲目（呼叫端就保留原本的曲子）。
        /// </summary>
        internal static bool Target(int channelIndex, int totalChannels, double now,
                                   out AudioClip clip, out float offset)
        {
            clip = null;
            offset = 0f;
            if (now < 0.0) return false;

            List<AudioClip> list = MusicLibrary.TracksFor(channelIndex, totalChannels);
            if (list == null || list.Count == 0) return false;

            double total = 0.0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] && list[i].length >= MinLength) total += list[i].length;
            if (total < MinLength) return false;

            double t = now % total;
            if (t < 0.0) t += total;   // 理論上 now 不會是負的，但別讓它變成負的偏移

            for (int i = 0; i < list.Count; i++)
            {
                AudioClip c = list[i];
                if (!c || c.length < MinLength) continue;

                if (t < c.length)
                {
                    clip = c;
                    // 夾一下：浮點誤差讓 offset 剛好等於 length 時 AudioSource.time 會拋。
                    offset = Mathf.Clamp((float)t, 0f, c.length - 0.01f);
                    return true;
                }
                t -= c.length;
            }

            // 浮點誤差把 t 推過了最後一首，退回最後一首的開頭。
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i] || list[i].length < MinLength) continue;
                clip = list[i];
                offset = 0f;
                return true;
            }
            return false;
        }
    }
}

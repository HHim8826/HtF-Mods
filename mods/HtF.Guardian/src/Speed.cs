using System.Collections.Generic;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 移動速度檢查（預設關閉）。
    ///
    /// 位置更新走的是 unreliable 通道，所以掉包、亂序、換島傳送、上船都會讓
    /// 「兩次更新之間的距離 ÷ 時間」暴衝。這裡的做法保守到近乎溫和：
    ///
    /// - 距離上一次**被接受**的更新超過 <see cref="Gap"/> 秒就當作斷過，
    ///   只重設基準不判定（掉包、載入、暫停回來都屬於這種）。
    /// - 一次超速不算數，要**持續**超速 <see cref="Grace"/> 秒才開始丟包
    ///   ——瞬間的抖動自己會過去。
    /// - 擋下來只是丟掉那個位置封包，不會踢人；下一個合法封包立刻恢復。
    ///
    /// **超速時基準（位置與時間）一起凍住，兩個都不動。** 這一點以前寫錯過：
    /// 原本只凍位置、時間照樣推進成 <c>now</c>，結果 <c>dt</c> 永遠是一個 frame、
    /// 距離卻永遠是「基準到現在」的總位移，算出來一定超速——被傳送的玩家會被
    /// **永久**擋住，連站著不動都救不回來，因為 <c>dt &gt; Gap</c> 那條逃生口
    /// 也被推進的時間戳堵死了。兩個一起凍住之後，<c>dt</c> 會隨時間變大、
    /// 算出的速度隨之下降，所以「其實只是延遲」會自己恢復，
    /// 真的傳送則最多在 <see cref="Gap"/> 秒後由重設基準那條路收掉。
    ///
    /// 這條規則的成本效益本來就不好（會誤判、擋不住小幅加速），
    /// 所以預設關著，留給「明知道有人在飛」的時候開。
    /// </summary>
    internal static class Speed
    {
        /// <summary>超過這麼久沒有被接受的更新，就當作斷過，重設基準。</summary>
        private const float Gap = 1.0f;

        /// <summary>持續超速多久才開始丟包。用時間而不是次數，才不受更新頻率影響。</summary>
        private const float Grace = 0.35f;

        private struct Sample
        {
            /// <summary>最後一次被接受的位置。</summary>
            internal Vector3 Pos;

            /// <summary>最後一次被接受的時間。**超速時不推進。**</summary>
            internal float Time;

            /// <summary>這一輪連續超速是什麼時候開始的。0 = 目前沒有在超速。</summary>
            internal float ViolatingSince;
        }

        private static readonly Dictionary<int, Sample> Samples = new Dictionary<int, Sample>();
        private static float _nextPrune;

        internal static bool Ok(Player player, Vector3 pos)
        {
            if (Plugin.CheckSpeed == null || !Plugin.CheckSpeed.Value) return true;
            if (!player) return true;

            float now = Time.unscaledTime;
            Prune(now);

            int key = player.GetInstanceID();
            Sample s;
            if (!Samples.TryGetValue(key, out s))
            {
                Samples[key] = new Sample { Pos = pos, Time = now };
                return true;
            }

            float dt = now - s.Time;
            if (dt <= 0f) return true;

            // 斷太久＝掉包、載入、暫停回來。沒有依據可以判定，重設基準。
            // 這也是傳送最後的收尾：撐過 Gap 秒就會被當成新的起點。
            if (dt > Gap)
            {
                Samples[key] = new Sample { Pos = pos, Time = now };
                return true;
            }

            if (Vector3.Distance(s.Pos, pos) / dt <= Plugin.MaxSpeed.Value)
            {
                // 接受：基準往前帶，連續超速的計時歸零。
                Samples[key] = new Sample { Pos = pos, Time = now };
                return true;
            }

            // 超速：基準整個凍住（位置**和**時間都不動），
            // 讓 dt 隨時間變大，算出的速度自己降下來。
            if (s.ViolatingSince == 0f) s.ViolatingSince = now;
            Samples[key] = s;

            return now - s.ViolatingSince < Grace;
        }

        private static void Prune(float now)
        {
            if (now < _nextPrune) return;
            _nextPrune = now + 30f;

            List<int> dead = null;
            foreach (var kv in Samples)
                if (now - kv.Value.Time > 60f) (dead ?? (dead = new List<int>())).Add(kv.Key);

            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++) Samples.Remove(dead[i]);
        }

        internal static void Clear() { Samples.Clear(); }
    }
}

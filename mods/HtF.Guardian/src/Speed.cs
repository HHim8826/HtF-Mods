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
    /// - 距離上一次更新超過 <see cref="Gap"/> 秒就當作斷過，只更新基準不判定
    ///   （掉包、載入、暫停回來都屬於這種）。
    /// - 一次超速不算數，要**連續**超過 <see cref="Strikes"/> 次才擋
    ///   ——真的在飛的人會一直超速，被傳送的人只會超一次。
    /// - 擋下來只是丟掉那個位置封包，不會踢人；下一個合法封包立刻恢復。
    ///
    /// 這條規則的成本效益本來就不好（會誤判、擋不住小幅加速），
    /// 所以預設關著，留給「明知道有人在飛」的時候開。
    /// </summary>
    internal static class Speed
    {
        private const float Gap = 1.0f;   // 超過這麼久沒更新就重設基準
        private const int Strikes = 5;    // 連續幾次超速才擋

        private struct Sample
        {
            internal Vector3 Pos;
            internal float Time;
            internal int Strikes;
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

            if (dt > Gap)
            {
                Samples[key] = new Sample { Pos = pos, Time = now };
                return true;
            }

            float speed = Vector3.Distance(s.Pos, pos) / dt;
            if (speed <= Plugin.MaxSpeed.Value)
            {
                Samples[key] = new Sample { Pos = pos, Time = now };
                return true;
            }

            s.Strikes++;
            s.Time = now;
            // 位置刻意不更新：基準留在最後一個「合理」的位置，
            // 不然作弊者每次小步跳，基準跟著跑就永遠不會超速。
            Samples[key] = s;
            return s.Strikes < Strikes;
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

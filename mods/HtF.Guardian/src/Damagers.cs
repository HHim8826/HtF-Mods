using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 「這隻生物最後是誰打的」。只服務一個守衛：<c>SetItemMultiplier</c>。
    ///
    /// 那條 RPC 沒有「操作者」可以驗——唯一的合法送出點是
    /// <c>Creature.LocalHit</c>（<c>Creature.cs:397</c>），擊殺者的客戶端替剛死的
    /// 生物設擊殺分數倍率，而擊殺者**既不是持有者也不是模擬者**
    /// （在水裡被射死的魚沒有人拿著）。所以照抄 <c>HeldBySender</c> 會把
    /// 正常擊殺全擋掉，而只驗「目標是一隻死掉的生物」又擋不住真正的攻擊：
    /// 倍率是**一次性**的（<c>Item.SetKillscoreMultiplier</c> 有
    /// <c>if (_killScoreMultiplier.Value != 1f) return;</c>），所以搶先對別人剛釣上來的
    /// 魚設 <c>0</c>，那條魚就永遠賣不出錢，而且補救不了。
    ///
    /// 補上的資訊就是這裡：<c>HitCreature</c> 的守衛本來就會看到「誰打了哪一隻」，
    /// 把它記下來，<c>SetItemMultiplier</c> 就能要求發送端是最後那個打的人。
    /// 遊戲的送出順序保證這件事成立——<c>LocalHit</c> 是先送 <c>HitCreature</c>
    /// 再送 <c>SetItemMultiplier</c>，兩條都是 Reliable 同序。
    /// </summary>
    internal static class Damagers
    {
        /// <summary>記錄的有效期。實際間隔是同一幀，這個窗口只是防呆。</summary>
        private const float Window = 10f;

        private struct Hit
        {
            internal int ClientId;
            internal float Time;
        }

        private static readonly Dictionary<int, Hit> Last = new Dictionary<int, Hit>();
        private static float _nextPrune;

        internal static void Record(Creature creature, NetworkConnection conn)
        {
            if (!creature || ReferenceEquals(conn, null)) return;
            float now = Time.unscaledTime;
            Prune(now);
            Last[creature.GetInstanceID()] = new Hit { ClientId = conn.ClientId, Time = now };
        }

        internal static bool IsLastDamager(Creature creature, NetworkConnection conn)
        {
            if (!creature || ReferenceEquals(conn, null)) return false;

            Hit hit;
            if (!Last.TryGetValue(creature.GetInstanceID(), out hit)) return false;
            if (Time.unscaledTime - hit.Time > Window) return false;
            return hit.ClientId >= 0 && hit.ClientId == conn.ClientId;
        }

        private static void Prune(float now)
        {
            if (now < _nextPrune) return;
            _nextPrune = now + 30f;

            List<int> dead = null;
            foreach (var kv in Last)
                if (now - kv.Value.Time > Window) (dead ?? (dead = new List<int>())).Add(kv.Key);

            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++) Last.Remove(dead[i]);
        }

        internal static void Clear() { Last.Clear(); }
    }
}

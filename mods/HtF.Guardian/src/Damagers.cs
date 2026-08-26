using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 「最近誰打過這隻生物」。只服務一個守衛：<c>SetItemMultiplier</c>。
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
    /// 把它記下來，<c>SetItemMultiplier</c> 就能要求發送端最近確實打過這隻生物。
    ///
    /// **記的是一份清單，不是「最後那一個人」。** 第一版每隻生物只留一筆，
    /// 誰後打就蓋掉誰——多人一起圍毆同一隻 Boss 或同一群魚時，那筆記錄會在
    /// 「A 送出 <c>HitCreature</c>」與「A 送出 <c>SetItemMultiplier</c>」之間
    /// 被 B 的非致命一擊蓋掉。兩條 RPC 之間隔著一次網路往返，中間插進別人的封包
    /// 是**常態而不是例外**：伺服器接著處理 A 的 <c>SetItemMultiplier</c> 時比對失敗，
    /// 判成「目標無效」，而那是會計入違規證據的類型（<c>CountsAsEvidence</c>），
    /// 累積下去會把完全正常的房客踢掉或封鎖。
    ///
    /// 所以改成記「<see cref="Window"/> 秒內打過這隻的所有人」，只要發送端在裡面就放行。
    /// 授權強度沒有變鬆多少——攻擊者一樣得先真的打到那隻生物才有資格設倍率，
    /// 而那正是唯一能拿來當身分的東西。
    /// </summary>
    internal static class Damagers
    {
        /// <summary>記錄的有效期。合法情境下兩條 RPC 幾乎是同一幀，這個窗口是給網路延遲的餘裕。</summary>
        private const float Window = 10f;

        /// <summary>同一隻生物最多記幾個攻擊者。純防呆——房間人數本來就遠小於這個數。</summary>
        private const int MaxDamagers = 32;

        private struct Hit
        {
            internal int ClientId;
            internal float Time;
        }

        /// <summary>生物的 InstanceID → 最近打過它的人。</summary>
        private static readonly Dictionary<int, List<Hit>> Recent = new Dictionary<int, List<Hit>>();
        private static float _nextPrune;

        internal static void Record(Creature creature, NetworkConnection conn)
        {
            if (!creature || ReferenceEquals(conn, null)) return;
            float now = Time.unscaledTime;
            Prune(now);

            int key = creature.GetInstanceID();
            List<Hit> hits;
            if (!Recent.TryGetValue(key, out hits)) Recent[key] = hits = new List<Hit>();

            for (int i = hits.Count - 1; i >= 0; i--)
            {
                if (hits[i].ClientId == conn.ClientId)
                {
                    hits[i] = new Hit { ClientId = conn.ClientId, Time = now };   // 同一個人：更新時間
                    return;
                }
                if (now - hits[i].Time > Window) hits.RemoveAt(i);                // 順手掃掉過期的
            }

            // 滿了就丟最舊的那個。ClientId 是伺服器端的連線編號、客戶端偽造不了，
            // 所以這裡不會被灌爆，留著只是不想讓清單無上限成長。
            if (hits.Count >= MaxDamagers) hits.RemoveAt(0);
            hits.Add(new Hit { ClientId = conn.ClientId, Time = now });
        }

        /// <summary>這個連線在 <see cref="Window"/> 秒內打過這隻生物嗎。</summary>
        internal static bool IsRecentDamager(Creature creature, NetworkConnection conn)
        {
            if (!creature || ReferenceEquals(conn, null)) return false;

            List<Hit> hits;
            if (!Recent.TryGetValue(creature.GetInstanceID(), out hits)) return false;

            float now = Time.unscaledTime;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].ClientId < 0 || hits[i].ClientId != conn.ClientId) continue;
                return now - hits[i].Time <= Window;
            }
            return false;
        }

        private static void Prune(float now)
        {
            if (now < _nextPrune) return;
            _nextPrune = now + 30f;

            List<int> dead = null;
            foreach (var kv in Recent)
            {
                List<Hit> hits = kv.Value;
                for (int i = hits.Count - 1; i >= 0; i--)
                    if (now - hits[i].Time > Window) hits.RemoveAt(i);

                if (hits.Count == 0) (dead ?? (dead = new List<int>())).Add(kv.Key);
            }

            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++) Recent.Remove(dead[i]);
        }

        internal static void Clear() { Recent.Clear(); }
    }
}

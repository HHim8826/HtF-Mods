using System.Collections.Generic;
using FishNet.Connection;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 每條連線、每個 RPC 一個權杖桶（token bucket）。
    ///
    /// 桶而不是「每秒計數」是有理由的：正常玩本來就會爆發性地送封包
    /// （一梭子彈、撿一整排東西），固定視窗計數會把那些正常行為切掉。
    /// 桶允許短時間爆發，長期平均仍受上限約束。
    ///
    /// 字典是兩層的（連線 → RPC 名稱 → 桶），刻意不用 <c>clientId + "|" + rpc</c>
    /// 當單層鍵：位置更新這種 RPC 每秒進來幾十次，那樣每次都要配置一個字串。
    /// </summary>
    internal static class Limiter
    {
        private sealed class Bucket
        {
            internal float Tokens;
            internal float Last;
        }

        private static readonly Dictionary<int, Dictionary<string, Bucket>> Buckets =
            new Dictionary<int, Dictionary<string, Bucket>>();

        internal static bool Allow(NetworkConnection conn, string rpc, float perSecond)
        {
            float mul = Plugin.RateMultiplier != null ? Plugin.RateMultiplier.Value : 1f;
            float rate = perSecond * mul;
            if (rate <= 0f) return true;

            // 桶的容量。至少留 5 個權杖，不然低速率的 RPC（例如買東西）
            // 會被正常的連按兩下打到。
            float burst = Mathf.Max(rate * 0.5f, 5f);

            int id = ReferenceEquals(conn, null) ? -1 : conn.ClientId;
            Dictionary<string, Bucket> perRpc;
            if (!Buckets.TryGetValue(id, out perRpc))
            {
                perRpc = new Dictionary<string, Bucket>(16);
                Buckets[id] = perRpc;
            }

            float now = Time.unscaledTime;
            Bucket b;
            if (!perRpc.TryGetValue(rpc, out b))
            {
                b = new Bucket { Tokens = burst, Last = now };
                perRpc[rpc] = b;
            }

            b.Tokens = Mathf.Min(burst, b.Tokens + (now - b.Last) * rate);
            b.Last = now;

            if (b.Tokens < 1f) return false;
            b.Tokens -= 1f;
            return true;
        }

        internal static void Forget(int clientId) { Buckets.Remove(clientId); }

        internal static void Clear() { Buckets.Clear(); }
    }
}

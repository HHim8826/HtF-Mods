using System;
using System.Reflection;
using HarmonyLib;

namespace HtF.HostRules
{
    /// <summary>
    /// 死亡與暈倒時，身上的東西去哪裡。
    ///
    /// 遊戲把「掉東西」分成**兩條完全不同的路徑**，兩個設定各對應一條：
    ///
    ///   暈倒（被打到 0 血，變成地上的 DeadPlayer，可以被別人救起）
    ///       PlayerVitals -> PlayerDying.ServerDie
    ///       方法最後那段只放掉**手上那一件**：SetSyncedHolder(null) + StartSimulateLocal。
    ///       背包完全不動。
    ///
    ///   死亡（放棄、重生）
    ///       Server.RpcLogic___RespawnPlayer -> PlayerInventory.ServerDropAll
    ///       手上的**加上整個背包**全部掉在地上。
    ///       而且全員陣亡時那條路會對**每一位玩家**各跑一次，不是只有放棄的那個人。
    ///
    /// 兩條都只在伺服器端跑（ServerDropAll 開頭就是 IsServerInitialized 檢查），
    /// 所以房主裝就夠，純客戶端裝了不會執行到。
    /// </summary>
    [HarmonyPatch]
    internal static class DeathRules
    {
        /// <summary>weaver 產生的方法名尾巴是簽章雜湊，遊戲改一次參數就會變，所以用前綴找。</summary>
        private const string RespawnPrefix = "RpcLogic___RespawnPlayer___";

        private static FieldInfo _dyingPlayer;
        private static MethodInfo _getOpenSlot;
        private static bool _resolved;

        /// <summary>現在正在 RespawnPlayer 的呼叫堆疊裡。</summary>
        private static bool _inRespawn;

        /// <summary>現在正在 ServerDie 的呼叫堆疊裡。</summary>
        private static bool _inServerDie;

        /// <summary>
        /// RespawnPlayer 的 patch 要手動掛：它是 weaver 產生的，名字帶雜湊，
        /// 沒辦法用 [HarmonyPatch(typeof, nameof)]。
        /// </summary>
        internal static void ApplyRespawnScope(Harmony harmony)
        {
            MethodInfo target = null;
            foreach (MethodInfo m in AccessTools.GetDeclaredMethods(typeof(Server)))
            {
                if (m.Name.StartsWith(RespawnPrefix, StringComparison.Ordinal)) { target = m; break; }
            }

            if (target == null)
            {
                Plugin.Log.LogWarning("找不到 Server." + RespawnPrefix + "*（遊戲可能又更新了），"
                                      + "「死亡不掉落背包」不會生效。");
                return;
            }

            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(DeathRules), nameof(Respawn_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(DeathRules), nameof(Respawn_Postfix))));
        }

        private static void Respawn_Prefix() { _inRespawn = true; }

        private static void Respawn_Postfix() { _inRespawn = false; }

        /// <summary>
        /// 死亡時整包掉在地上的那一步。
        ///
        /// **只擋從 RespawnPlayer 進來的那一次**，不是把 ServerDropAll 整個關掉——
        /// 同一個方法也是 DropAllItems 這條 ServerRpc 的落點（HtF.DazedTools 的
        /// 「掉光所有物品」指令就走那裡）。整個關掉會把一個刻意送出的指令也一起弄壞。
        /// 用呼叫堆疊上的旗標分辨，和 HtF.Guardian 的 Sender.Begin/End 同一招。
        /// </summary>
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.ServerDropAll))]
        [HarmonyPrefix]
        private static bool ServerDropAll_Prefix()
        {
            return !(_inRespawn && Plugin.KeepInventoryOnDeath.Value);
        }

        /// <summary>
        /// 暈倒時把手上那件收進背包，而不是讓它掉在地上。
        ///
        /// **為什麼是「收進背包」而不是「留在手上」**：留在手上做不到。倒下的那個人
        /// 自己的客戶端會跑 PlayerDying.LocalDie，裡面無條件對 HeldItem 呼叫
        /// Drop(false)，那是純本機的表現（calledFromLocal = false 那條路不送 RPC），
        /// 房主端擋不到別人機器上的那一行。
        ///
        /// 但 LocalDie 的第一件事是 Inventory.ApplySlot(-1)，而 ApplySlot 對
        /// **已經在背包裡**的手持物走的是收納那條路（Hands.DropItem + SetHeldItem(null)），
        /// 走完 HeldItem 就是 null，後面那個 Drop 根本不會執行。所以只要伺服器端
        /// 先把它塞進背包，客戶端自己就不會演出掉落。
        ///
        /// [未驗證] 這中間有競態：LocalDie 是客戶端用本機血量預測觸發的，可能比
        /// 背包的同步先到，那一瞬間會看到東西掉出來、同步追上後才回到背包。
        /// 實際上有多明顯要進遊戲才知道。
        /// </summary>
        [HarmonyPatch(typeof(PlayerDying), nameof(PlayerDying.ServerDie))]
        [HarmonyPrefix]
        private static void ServerDie_Prefix(PlayerDying __instance)
        {
            if (!Plugin.KeepHeldItemWhenDowned.Value) return;

            _inServerDie = true;                       // 給下面 SetSyncedHolder 的守衛用
            try { StashHeldItem(__instance); }
            catch (Exception e) { Plugin.Log.LogError("暈倒時收納手持物失敗：" + e); }
        }

        [HarmonyPatch(typeof(PlayerDying), nameof(PlayerDying.ServerDie))]
        [HarmonyPostfix]
        private static void ServerDie_Postfix() { _inServerDie = false; }

        /// <summary>
        /// ServerDie 的最後一段會 SetSyncedHolder(null)，而那個方法**會先把物品從背包裡
        /// 移除**再把持有者清成 null（Item.cs 的 RemoveItem 分支）。也就是說只在 prefix
        /// 把東西塞進背包沒有用，原方法自己會把它撤銷掉——所以這一段也要擋。
        ///
        /// 範圍收得很窄：只有在 ServerDie 的堆疊裡、而且是「把持有者清成 null」那一種
        /// 呼叫才擋。其餘所有交接、撿拾、丟棄都照常。
        /// </summary>
        [HarmonyPatch(typeof(Item), nameof(Item.SetSyncedHolder))]
        [HarmonyPrefix]
        private static bool SetSyncedHolder_Prefix(Player newHolder)
        {
            return !(_inServerDie && newHolder == null);
        }

        private static void StashHeldItem(PlayerDying dying)
        {
            Resolve();
            if (_dyingPlayer == null || _getOpenSlot == null) return;

            Player player = _dyingPlayer.GetValue(dying) as Player;
            if (player == null || player.Holding == null) return;

            Item held = player.Holding.HeldItem;
            if (!held || held.IsInInventory) return;

            // AddItem 自己會擋 SyncedHolder 不是這個玩家的情況，但先問一次比較好讀
            if (held.SyncedHolder != player) return;

            int slot = (int)_getOpenSlot.Invoke(player.Inventory, new object[] { 0 });
            if (slot < 0)
            {
                // 背包滿了就照原本的規則掉在地上。這不是失敗，是沒有地方放。
                Plugin.Log.LogInfo("背包沒有空位，手上的「" + held.name + "」還是會掉出來。");
                _inServerDie = false;          // 讓原本那段照跑
                return;
            }

            player.Inventory.AddItem((byte)slot, held);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _dyingPlayer = AccessTools.Field(typeof(PlayerDying), "_player");
            _getOpenSlot = AccessTools.Method(typeof(PlayerInventory), "GetOpenSlot");

            if (_dyingPlayer == null || _getOpenSlot == null)
                Plugin.Log.LogWarning("找不到 PlayerDying._player 或 PlayerInventory.GetOpenSlot"
                                      + "（遊戲可能又更新了），「暈倒不掉手上的東西」不會生效。");
        }
    }
}

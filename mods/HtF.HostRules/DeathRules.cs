using System;
using System.Reflection;
using HarmonyLib;

namespace HtF.HostRules
{
    /// <summary>
    /// 死亡時背包的去向。
    ///
    /// 遊戲掉東西走的是**兩條互不相干的路**：
    ///
    ///   暈倒（被打到 0 血，變成地上的 DeadPlayer，可以被別人救起）
    ///       PlayerVitals -> PlayerDying.ServerDie
    ///       方法最後那段只放掉**手上那一件**，背包完全不動。
    ///
    ///   死亡（放棄、重生）
    ///       Server.RpcLogic___RespawnPlayer -> PlayerInventory.ServerDropAll
    ///       手上的**加整個背包**全掉在地上。
    ///       而且全員陣亡時那條路會對**每一位玩家**各跑一次，不是只有放棄的那個人。
    ///
    /// **這裡只管第二條。** 第一條試過，做不到，理由記在下面，不要再試同一招。
    ///
    /// ── 為什麼「暈倒不掉手上的東西」做不到（房主端）───────────────────
    ///
    /// 倒下的人**自己的客戶端**會跑 PlayerDying.LocalDie，裡面無條件對 HeldItem
    /// 呼叫 Drop(false, ...)。那是純本機的表現（calledFromLocal = false 那條路不送
    /// RPC），房主端擋不到別人機器上的那一行。
    ///
    /// 曾經以為有繞法：LocalDie 的第一行是 Inventory.ApplySlot(-1)，而收納那條路
    /// （Hands.DropItem + Holding.SetHeldItem(null)）走完之後 HeldItem 就是 null，
    /// 後面那個 Drop 不會執行。只要伺服器端搶先把物品塞進背包就好——**這個推論是錯的**：
    ///
    ///   1. ApplySlot（PlayerInventory.cs:513-532）只切 _localCurSlot 然後送
    ///      Server.SelectInvSlot。收納其實在 UpdateHeldItem（同檔 637-642），
    ///      要經過 ServerRpc -> SyncVar OnChange 一整趟網路來回才到得了，
    ///      而 LocalDie 的 Drop 是同一個呼叫堆疊裡的下一行，一定先跑。
    ///   2. 就算等得到，OnCurSlotChange 開頭是 `if (prev == -1 && next == -1) return;`。
    ///      從地上撿起來拿在手上時 prev 本來就是 −1（Item.cs:996 撿起時已經
    ///      ServerSetSyncedCurSlot(-1) 過），傳 −1 進去等於什麼都沒發生。
    ///   3. PlayerInventory.AddItem 只寫 _items[index] = item，不設 IsInInventory
    ///      （那是 Item.PutInInventory 做的）、也不清 Holding.HeldItem。
    ///
    /// 硬做的結果是一件**永久撿不回來**的物品：_syncedHolder 沒被清掉，所以
    /// Item.SetSyncedHolder 第一道守衛（Item.cs:976）擋掉所有新持有者；而
    /// IsInInventory 是 false、Holder 是 null，所以 UpdateHeldItem（PlayerInventory.cs:622）
    /// 會把它抹成 null，自己也拿不回來。**比原本掉在地上可以撿回來還糟。**
    ///
    /// 要做這件事，得讓每個客戶端也裝一份東西去攔它自己的 LocalDie，
    /// 那就不是「房主規則」了。
    /// </summary>
    [HarmonyPatch]
    internal static class DeathRules
    {
        /// <summary>weaver 產生的方法名尾巴是簽章雜湊，遊戲改一次參數就會變，所以用前綴找。</summary>
        private const string RespawnPrefix = "RpcLogic___RespawnPlayer___";

        /// <summary>現在正在 RespawnPlayer 的呼叫堆疊裡。</summary>
        private static bool _inRespawn;

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
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(DeathRules), nameof(Respawn_Finalizer))));
        }

        private static void Respawn_Prefix() { _inRespawn = true; }

        /// <summary>
        /// **必須是 finalizer，不能是 postfix。** postfix 在原方法丟例外時不會執行，
        /// 而 RpcLogic___RespawnPlayer 裡有 BoatManager.Instance.TryMoveBoat 這種會丟的東西。
        /// 一旦漏放，_inRespawn 這輩子都放不下來（prefix 只設 true），
        /// ServerDropAll 從此永遠被擋——連 DazedTools 的「掉光所有物品」也一起壞掉，
        /// 而那正是下面那個 patch 特地不去整個關掉 ServerDropAll 想保護的東西。
        ///
        /// 回傳 void 的 finalizer 不會吞掉例外，只是保證跑得到。
        /// </summary>
        private static void Respawn_Finalizer() { _inRespawn = false; }

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
    }
}

using System;
using FishNet.Connection;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>
    /// 每個危險 <c>RpcLogic___*</c> 的 prefix。
    ///
    /// **參數一律用 Harmony 的位置注入 <c>__0</c>、<c>__1</c>…**，不是用名字。
    /// FishNet 的 IL weaver 產生的 <c>RpcLogic___*</c> 參數在 metadata 裡沒有名字
    /// （反編譯出來是 <c>A_1</c>、<c>A_2</c>，那是 dnSpy 對無名參數的填充），
    /// 所以按名字綁一定失敗。位置注入還有一個好處：遊戲改參數名不會影響我們。
    /// 要改寫參數就宣告成 <c>ref</c>。
    ///
    /// 回傳 <c>false</c> = 不執行原方法（擋下這次呼叫）。
    ///
    /// **每個守衛都必須不丟例外。** FishNet 把「RPC 執行期間丟例外」當成惡意封包，
    /// 會直接踢掉發送者（`AI_CONTEXT.md` 第 3 節）——守衛自己的 bug 不該變成踢人。
    /// 所以這裡只做 null 檢查、比大小、比連線，沒有任何會丟的操作；
    /// 需要反射或掃場景的部分都關在 <see cref="Prices"/> 自己的 try/catch 裡。
    ///
    /// 每個方法第一行都是 <c>if (Sender.Exempt) return true;</c>：
    /// 總開關關掉、或發送端是房主自己（預設不檢查）時整條路直接讓開。
    /// </summary>
    internal static class Guards
    {
        // ================================================================== reader：接住發送端

        /// <summary>
        /// 每個 <c>RpcReader___*</c> 的 prefix。第三個參數就是 FishNet 填的發送端連線。
        /// 這是整個 mod 唯一的資訊來源——沒有它，所有守衛都無從判斷「誰在操作」。
        /// </summary>
        internal static void ReaderPrefix(NetworkConnection __2) { Sender.Begin(__2); }

        internal static void ReaderPostfix() { Sender.End(); }

        // ================================================================== 面板

        /// <summary>
        /// 面板開著的時候把本機玩家的輸入擋掉（移動、視角、開火、拿取…）。
        /// <c>Player.BlockInputs</c> 被相機、持有、移動、船等各處消費，一個點就全擋住。
        /// 它是五個條件的計算屬性，不是一行的 auto-property，所以 patch 是可靠的
        /// （見 `MODDING_CONTEXT.md` 第 3.2 節那條 inline 規則）。
        /// </summary>
        internal static void BlockInputs(Player __instance, ref bool __result)
        {
            if (Panel.Visible && __instance == Player.LocalPlayer) __result = true;
        }

        // ================================================================== 共用判斷

        private static bool Id { get { return Plugin.CheckIdentity.Value; } }
        private static bool Val { get { return Plugin.CheckValues.Value; } }
        private static bool Idx { get { return Plugin.CheckIndices.Value; } }

        /// <summary>這個物品目前在發送端手上（或背包裡）嗎。</summary>
        private static bool HeldBySender(Item item)
        {
            if (!item) return false;
            // SyncedHolder 是 SyncVar，伺服器端一定是對的；_holder 是本機狀態，
            // 房主身上也有（OnSyncedHolderChange 的 asServer==false 那一輪會設），
            // 但拿它當備援就好。
            Player owner = item.SyncedHolder;
            if (!owner) owner = item.Holder;
            return Sender.Owns(owner);
        }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }

        // ================================================================== 玩家身分

        // 生成玩家。遊戲自己已經驗過 Steam 身分（RpcLogic___SpawnPlayer 是唯一
        // 有用到 conn 的），這裡補的是「一條連線只能有一個 Player」——原本沒有這個
        // 檢查，重複送就會多生一隻，而多出來的那些永遠不會被回收。
        internal static bool SpawnPlayer()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SpawnPlayer", 1f)) return false;

            if (Bans.IsBanned(Sender.SteamId)) return G.Deny("SpawnPlayer", Why.已封鎖);
            if (Id && Sender.Player) return G.Deny("SpawnPlayer", Why.重複生成);
            return true;
        }

        internal static bool RespawnPlayer(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("RespawnPlayer", 5f)) return false;
            // 全員死亡時這條 RPC 會丟掉每個人的背包、重置每個人的數值、把船搬回出生點。
            // 身分沒對就是大規模搗亂。
            if (Id && !Sender.Owns(__0)) return G.Deny("RespawnPlayer", Why.身分不符);
            return true;
        }

        internal static bool TeleportPlayer(Player __0, Vector3 __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("TeleportPlayer", 10f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("TeleportPlayer", Why.身分不符);
            if (Val && !Finite(__1)) return G.Deny("TeleportPlayer", Why.數值超出範圍);
            return true;
        }

        internal static bool UpdatePlayerPosRot(Player __0, Vector3 __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdatePlayerPosRot", 120f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("UpdatePlayerPosRot", Why.身分不符);
            if (Val && !Finite(__1)) return G.Deny("UpdatePlayerPosRot", Why.數值超出範圍);
            if (!Speed.Ok(__0, __1)) return G.Deny("UpdatePlayerPosRot", Why.移動過快);
            return true;
        }

        internal static bool UpdatePlayerCrouching(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdatePlayerCrouching", 20f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("UpdatePlayerCrouching", Why.身分不符);
            return true;
        }

        internal static bool SetIsAfk(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetIsAfk", 10f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("SetIsAfk", Why.身分不符);
            return true;
        }

        internal static bool SendFinishedTutorial(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SendFinishedTutorial", 5f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("SendFinishedTutorial", Why.身分不符);
            return true;
        }

        // 復活別人是合作行為，本來就該由別人來做，所以不驗身分——
        // 只確認送出的人自己在場上，並限速。
        internal static bool ResurrectPlayer()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ResurrectPlayer", 5f)) return false;
            if (Id && !Sender.Player) return G.Deny("ResurrectPlayer", Why.目標無效);
            return true;
        }

        internal static bool UpdateDeadPlayerResurrectPercent(ref float __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdateDeadPlayerResurrectPercent", 40f)) return false;
            if (!Val) return true;
            if (!Finite(__1)) return G.Deny("UpdateDeadPlayerResurrectPercent", Why.數值超出範圍);
            __1 = Mathf.Clamp01(__1);
            return true;
        }

        // ================================================================== 傷害

        internal static bool HitCreature(Creature __0, Player __1, int __2)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("HitCreature", 60f)) return false;
            if (Id && !Sender.Owns(__1)) return G.Deny("HitCreature", Why.身分不符);
            // 負傷害會走 Creature.ServerChangeHp 的 `_hp.Value -= damage`，等於替生物回血。
            // 0 放行：伺服器端本來就是無動作，擋它只會製造假違規。
            if (Val && (__2 < 0 || __2 > Plugin.MaxCreatureDamage.Value))
                return G.Deny("HitCreature", Why.數值超出範圍);
            return true;
        }

        internal static bool HitPlayer(Player __0, int __1, Player __5)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("HitPlayer", 60f)) return false;

            if (__5)
            {
                // 有指定攻擊者：那個攻擊者必須就是送封包的人。
                // 遊戲自己的送出點（PlayerVitals.LocalHit）前面就有
                // `if (!playerWhoHit.Owner.IsLocalClient) return;`，所以恆成立。
                if (Id && !Sender.Owns(__5)) return G.Deny("HitPlayer", Why.身分不符);
                // 0 放行：RpcLogic___HitPlayer 本來就是 `if (A_2 != 0)` 才動作。
                if (Val && (__1 < 0 || __1 > Plugin.MaxPlayerDamage.Value))
                    return G.Deny("HitPlayer", Why.數值超出範圍);
                return true;
            }

            // 沒有攻擊者＝環境傷害（溺水、生物撞擊、爆炸）。遊戲允許任何客戶端
            // 代生物送出這種封包（AttackingFish.DamageOnCollision 只看
            // `_rigSync.IsSimulatedLocal`），所以沒有身分可以驗——
            // 而 RpcLogic___HitPlayer 的友傷檢查是 `if (A_6 && !UseFriendlyFire) return;`，
            // 攻擊者傳 null 就整個跳過。能做的只有夾上限加限速，這是已知的殘留風險。
            if (!G.Rate("HitPlayer.無來源", 40f)) return false;
            if (Val && (__1 < 0 || __1 > Plugin.MaxSourcelessDamage.Value))
                return G.Deny("HitPlayer", Why.數值超出範圍);
            return true;
        }

        internal static bool ActivateExplosive(Player __4)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ActivateExplosive", 10f)) return false;
            // __4 是「誰逼它提早爆」，正常流程要嘛是 null（引信自然到期），
            // 要嘛是本機玩家（Explosive.ForceExplode）。
            if (Id && __4 && !Sender.Owns(__4)) return G.Deny("ActivateExplosive", Why.身分不符);
            return true;
        }

        internal static bool Punch(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("Punch", 20f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("Punch", Why.身分不符);
            return true;
        }

        internal static bool MeleeAttack(Melee __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("MeleeAttack", 20f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("MeleeAttack", Why.不是持有者);
            return true;
        }

        // ================================================================== 武器與彈道

        internal static bool AddProjectile(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("AddProjectile", 60f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("AddProjectile", Why.身分不符);
            return true;
        }

        internal static bool AddProjectiles(Player __0, Vector3[] __5)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("AddProjectiles", 30f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("AddProjectiles", Why.身分不符);
            if (Val && __5 != null && __5.Length > Plugin.MaxProjectilesPerShot.Value)
                return G.Deny("AddProjectiles", Why.數值超出範圍);
            return true;
        }

        internal static bool ProjectileHitDynamic(ref NetworkConnection __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ProjectileHitDynamic", 120f)) return false;
            // 命中歸屬只能是自己。遊戲的送出點（ProjectileManager.Hit）本來送的
            // 就是 projectile.Owner.Owner，而它只在 projectile.IsLocal 時才送。
            if (Id && !Sender.Same(__0, Sender.Current))
            {
                __0 = Sender.Current;
                G.Flag("ProjectileHitDynamic.冒名", Why.身分不符);
            }
            return true;
        }

        internal static bool ReloadWeapon(Weapon __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ReloadWeapon", 10f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("ReloadWeapon", Why.不是持有者);
            return true;
        }

        internal static bool InspectTool(Tool __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("InspectTool", 10f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("InspectTool", Why.不是持有者);
            return true;
        }

        internal static bool UpdateHeldToolPosRot(Tool __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdateHeldToolPosRot", 120f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("UpdateHeldToolPosRot", Why.不是持有者);
            return true;
        }

        // ================================================================== 物品

        internal static bool SetItemHolder(Item __0, Player __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetItemHolder", 20f)) return false;
            if (!Id) return true;

            if (__1)
            {
                // 撿起來：只能撿到自己手上。遊戲另外已經擋掉「搶別人手上的東西」
                // （syncedHolder 不是目標時走 TargetReconcileRejectedItemPickup）。
                if (!Sender.Owns(__1)) return G.Deny("SetItemHolder", Why.身分不符);
                return true;
            }

            // 放下：只能放下自己拿著的東西。還沒有持有者的物品也放行——
            // 那是遊戲用來把殘留狀態清乾淨的正常路徑。
            Player holder = __0 ? __0.SyncedHolder : null;
            if (holder && !Sender.Owns(holder)) return G.Deny("SetItemHolder", Why.不是持有者);
            return true;
        }

        internal static bool PutItemInInventory(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("PutItemInInventory", 30f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("PutItemInInventory", Why.身分不符);
            return true;
        }

        internal static bool RemoveItemFromInventory(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("RemoveItemFromInventory", 30f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("RemoveItemFromInventory", Why.身分不符);
            return true;
        }

        internal static bool DropAllItems(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("DropAllItems", 5f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("DropAllItems", Why.身分不符);
            return true;
        }

        internal static bool SelectInvSlot(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SelectInvSlot", 30f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("SelectInvSlot", Why.身分不符);
            return true;
        }

        internal static bool SetItemSkin(Item __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetItemSkin", 10f)) return false;
            // 遊戲只對自己手上的物品送這條（PlayerHolding.cs:183）。
            if (Id && !HeldBySender(__0)) return G.Deny("SetItemSkin", Why.不是持有者);
            return true;
        }

        internal static bool SetItemMultiplier(ref float __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetItemMultiplier", 20f)) return false;
            if (!Val) return true;
            if (!Finite(__1) || __1 < 0f || __1 > Plugin.MaxScoreMultiplier.Value)
                return G.Deny("SetItemMultiplier", Why.數值超出範圍);
            return true;
        }

        internal static bool GrillItemInLava()
        {
            if (Sender.Exempt) return true;
            return G.Rate("GrillItemInLava", 30f);
        }

        internal static bool PlayImpactSound()
        {
            if (Sender.Exempt) return true;
            return G.Rate("PlayImpactSound", 60f);
        }

        internal static bool HandOverItemSimulation()
        {
            if (Sender.Exempt) return true;
            return G.Rate("HandOverItemSimulation", 30f);
        }

        internal static bool UpdateItemPosRot(ref NetworkConnection __1)
        {
            if (Sender.Exempt) return true;
            // 全場物品的位置同步都走這一條，是流量最大的 RPC。
            if (!G.Rate("UpdateItemPosRot", 400f)) return false;

            // 模擬權的宣稱只能是自己。RigidbodySync.ServerSetPosRot 用這個連線做
            // 回音判斷，讓客戶端自己填等於可以冒名搬東西（`/tpitems` 就是這樣做的）。
            // 遊戲自己送的一律是 InstanceFinder.ClientManager.Connection
            // （RigidbodySync.cs:730、735、738），所以蓋成發送端對正常流量是無變化的。
            if (Id && !Sender.Same(__1, Sender.Current))
            {
                __1 = Sender.Current;
                G.Flag("UpdateItemPosRot.冒名", Why.身分不符);
            }
            return true;
        }

        internal static bool SetSyncedSimulator(ref NetworkConnection __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetSyncedSimulator", 60f)) return false;
            if (!Id) return true;

            if (Sender.Same(__1, Sender.Current)) return true;

            // 「把模擬權交還給伺服器」是正常路徑，不能一律蓋成發送端：
            // RigidbodySync.cs:532 停止模擬時送的是 Server.Instance.Owner，
            // 那條連線是伺服器自己的（或是 ClientId < 0 的空連線）。
            if (ReferenceEquals(__1, null) || __1.ClientId < 0) return true;
            if (Sender.Same(__1, ServerOwner())) return true;

            __1 = Sender.Current;
            G.Flag("SetSyncedSimulator.冒名", Why.身分不符);
            return true;
        }

        private static NetworkConnection ServerOwner()
        {
            try { return Server.Instance ? Server.Instance.Owner : null; }
            catch (Exception) { return null; }
        }

        // ================================================================== 釣魚

        internal static bool UpdateBaitPosAndLineLength(FishingRod __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdateBaitPosAndLineLength", 120f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("UpdateBaitPosAndLineLength", Why.不是持有者);
            return true;
        }

        internal static bool UpdateRodPullBack(FishingRod __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdateRodPullBack", 120f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("UpdateRodPullBack", Why.不是持有者);
            return true;
        }

        internal static bool ReleaseItemFromBait(FishingRod __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ReleaseItemFromBait", 10f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("ReleaseItemFromBait", Why.不是持有者);
            return true;
        }

        internal static bool ChangeBait(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ChangeBait", 20f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("ChangeBait", Why.身分不符);
            return true;
        }

        // ================================================================== 經濟

        internal static bool BuyItem(Player __1, ref bool __5)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyItem", 10f)) return false;
            if (Id && !Sender.Owns(__1)) return G.Deny("BuyItem", Why.身分不符);
            // isFree 是客戶端傳的布林，true 就整個跳過扣款——遊戲自己只有
            // DazedCommands（開發者作弊指令）會傳 true，正常商店一律 false。
            if (Plugin.CheckPrices.Value && __5)
            {
                __5 = false;
                G.Flag("BuyItem.免費旗標", Why.價格不符);   // 記一筆，但仍然讓它買（照價扣款）
            }
            return true;
        }

        internal static bool UnlockPocket(Player __0, byte __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UnlockPocket", 5f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("UnlockPocket", Why.身分不符);
            // PlayerInventory.GetExtraSlotCost 是 `_extraSlotCosts[index - 1]`，
            // byte 0 在減法時提升成 int 變 −1（不是 255）直接越界。
            // 遊戲只擋了上界（A_2 > 5），沒擋下界。
            if (Idx && (__1 < 1 || __1 > 5)) return G.Deny("UnlockPocket", Why.索引超出範圍);
            return true;
        }

        internal static bool BuyBait(Player __0, byte __1, ref int __2)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyBait", 10f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("BuyBait", Why.身分不符);

            // ServerBoughtBait 也是 `_ownedBaits[index - 1]`，同一個 index−1 陷阱。
            if (Idx && __1 < 1) return G.Deny("BuyBait", Why.索引超出範圍);

            if (Plugin.CheckPrices.Value)
            {
                int expected;
                if (!Prices.BaitCost(__1, __2, out expected))
                {
                    __2 = expected;
                    G.Flag("BuyBait.價格", Why.價格不符);
                }
            }
            return true;
        }

        internal static bool BuyBoatMotor(Player __0, byte __1, ref int __2)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyBoatMotor", 10f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("BuyBoatMotor", Why.身分不符);

            if (Plugin.CheckPrices.Value)
            {
                int expected;
                if (!Prices.MotorCost(__1, __2, out expected))
                {
                    __2 = expected;
                    G.Flag("BuyBoatMotor.價格", Why.價格不符);
                }
            }
            return true;
        }

        internal static bool BuyBoatRadar(Player __0, ref int __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyBoatRadar", 10f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("BuyBoatRadar", Why.身分不符);

            if (Plugin.CheckPrices.Value)
            {
                int expected;
                if (!Prices.RadarCost(__1, out expected))
                {
                    __1 = expected;
                    G.Flag("BuyBoatRadar.價格", Why.價格不符);
                }
            }
            return true;
        }

        internal static bool BuyAttachment(Weapon __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyAttachment", 10f)) return false;
            // 價格是伺服器自己算的，這裡只擋「幫別人的槍買配件、花全隊的錢」。
            if (Id && !HeldBySender(__0)) return G.Deny("BuyAttachment", Why.不是持有者);
            return true;
        }

        internal static bool BuyBulletUpgrade(Weapon __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuyBulletUpgrade", 10f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("BuyBulletUpgrade", Why.不是持有者);
            return true;
        }

        internal static bool BuySharpnessUpgrade(Melee __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("BuySharpnessUpgrade", 10f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("BuySharpnessUpgrade", Why.不是持有者);
            return true;
        }

        internal static bool TakeItemFromNpc(Player __0, byte __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("TakeItemFromNpc", 5f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("TakeItemFromNpc", Why.身分不符);
            // 遊戲的守衛是 `if (A_2 != 255 && !NpcIsHoldingItem(A_2)) return;`
            // ——id 剛好是 255 時整個守衛被跳過，接著的 GetShowingQuest 會走到
            // 字典索引器 `_idToNpc[255]` 丟 KeyNotFoundException。
            if (Idx && __1 == 255) return G.Deny("TakeItemFromNpc", Why.索引超出範圍);
            return true;
        }

        // ================================================================== 賭場

        internal static bool PlaceBet()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("PlaceBet", 5f)) return false;
            if (Idx && !CasinoAlive()) return G.Deny("PlaceBet", Why.目標無效);
            return true;
        }

        internal static bool UpdateRoulette()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("UpdateRoulette", 60f)) return false;
            if (Idx && !CasinoAlive()) return G.Deny("UpdateRoulette", Why.目標無效);
            return true;
        }

        /// <summary>
        /// <c>CasinoManager.Instance</c> 是純靜態欄位，只在 Awake 指派、銷毀時不清空
        /// （CasinoManager.cs:342）。離開賭場島之後它是「已銷毀但參照還在」的 Unity 物件，
        /// 存取會丟 MissingReferenceException，而那會讓發送者被踢掉。
        /// Unity 的 <c>bool</c> 轉換正是用來分辨這種假 null 的。
        /// </summary>
        private static bool CasinoAlive()
        {
            try { return CasinoManager.Instance; }
            catch (Exception) { return false; }
        }

        // ================================================================== 船

        internal static bool SetDriver(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetDriver", 5f)) return false;
            if (!Id) return true;

            if (__0) return Sender.Owns(__0) ? true : G.Deny("SetDriver", Why.身分不符);

            // 傳 null 是下船。只有現任駕駛能下船——不然任何人都能把別人踹下駕駛座。
            Player driver = CurrentDriver();
            if (driver && !Sender.Owns(driver)) return G.Deny("SetDriver", Why.不是駕駛);
            return true;
        }

        internal static bool SendBoatInput()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SendBoatInput", 120f)) return false;
            if (!Id) return true;

            Player driver = CurrentDriver();
            if (driver && !Sender.Owns(driver)) return G.Deny("SendBoatInput", Why.不是駕駛);
            return true;
        }

        private static Player CurrentDriver()
        {
            try { return BoatManager.Boat ? BoatManager.Boat.Driver : null; }
            catch (Exception) { return null; }
        }

        internal static bool SetBoatSkin()
        {
            if (Sender.Exempt) return true;
            return G.Rate("SetBoatSkin", 5f);
        }

        // ================================================================== 其他

        internal static bool SendChatMessage(ref ulong __0, ref string __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SendChatMessage", 3f)) return false;

            if (Id)
            {
                // `from` 是客戶端自填的 SteamID，冒名發言只要換個數字。
                // 連線位址是傳輸層給的，偽造不了——直接用它蓋掉。
                ulong real = Sender.SteamId;
                if (real != 0UL && __0 != real)
                {
                    __0 = real;
                    G.Flag("SendChatMessage.冒名", Why.冒用身分);   // 記一筆，訊息仍以正確身分送出
                }
            }

            int max = Plugin.MaxChatLength.Value;
            if (Val && !string.IsNullOrEmpty(__1) && __1.Length > max) __1 = __1.Substring(0, max);
            return true;
        }

        internal static bool FinishEatingCreature(Player __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("FinishEatingCreature", 10f)) return false;
            if (Id && !Sender.Owns(__1)) return G.Deny("FinishEatingCreature", Why.身分不符);
            return true;
        }

        internal static bool ToggleEatCreature(Player __0)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("ToggleEatCreature", 20f)) return false;
            if (Id && !Sender.Owns(__0)) return G.Deny("ToggleEatCreature", Why.身分不符);
            return true;
        }

        internal static bool SetRadioFrequency(Radio __0, float __1)
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SetRadioFrequency", 40f)) return false;
            if (Id && !HeldBySender(__0)) return G.Deny("SetRadioFrequency", Why.不是持有者);
            // Radio.SetFrequency 自己會夾範圍，這裡只要擋掉 NaN／無限大。
            if (Val && !Finite(__1)) return G.Deny("SetRadioFrequency", Why.數值超出範圍);
            return true;
        }

        internal static bool ClientSpokeToNpc()
        {
            if (Sender.Exempt) return true;
            return G.Rate("ClientSpokeToNpc", 10f);
        }

        internal static bool SendFinishGame()
        {
            if (Sender.Exempt) return true;
            if (!G.Rate("SendFinishGame", 2f)) return false;
            // 遊戲原本任何人都能結束整場遊戲。這是設定，因為合作模式下
            // 「誰都能按結束」也可能是刻意的設計。
            if (Plugin.FinishGameHostOnly.Value) return G.Deny("SendFinishGame", Why.限房主);
            return true;
        }
    }
}

using System.Collections.Generic;
using HtF.Shared;

namespace HtF.DazedTools.UI
{
    internal enum ArgKind
    {
        Text, Int, Float, Bool, Flag,
        Player, Item, Bait, Attachment, Pocket, Motor, Npc, Roulette,
        Vec3, PosOrVoid,
    }

    /// <summary>指令的影響範圍，決定 UI 的顏色與是否要二次確認。</summary>
    internal enum Risk
    {
        Safe,     // 只影響自己
        Caution,  // 影響其他玩家，或有封包量／踢線風險
        Danger,   // 會結束整場遊戲、動 Steam 成就，或有已知自踢陷阱
    }

    /// <summary>
    /// 一個參數。文字欄位都是中英一對——畫的時候才用 <see cref="Loc.P"/> 挑，
    /// 送出去的指令字串完全不受語言影響（那是遊戲的指令語法，永遠是英文的）。
    /// </summary>
    internal sealed class Arg
    {
        public string Label;
        public string LabelEn;
        public ArgKind Kind;
        public string Default = "";   // 送出時若此格為空、但後面還有值，就補這個
        public string Token = "";     // Flag 專用：勾選時送出的字面值
        public bool AllowAll;         // Player 專用
        public bool AllowNone;        // Player 專用（setboatdriver 的 none）
        public string Hint = "";
        public string HintEn = "";

        public Arg(string label, string labelEn, ArgKind kind) { Label = label; LabelEn = labelEn; Kind = kind; }

        public string Text { get { return Loc.P(Label, LabelEn ?? Label); } }
        public string HintText { get { return Loc.P(Hint, string.IsNullOrEmpty(HintEn) ? Hint : HintEn); } }
    }

    internal sealed class Cmd
    {
        public string Name;
        public string Category;
        public string Summary;
        public string SummaryEn;
        public Risk Risk = Risk.Safe;
        public bool Common;           // 是否出現在「常用」分頁
        public string Warning = "";   // 紅字提示
        public string WarningEn = "";
        public Arg[] Args = new Arg[0];

        public Cmd(string name, string category, string summary, string summaryEn)
        {
            Name = name; Category = category; Summary = summary; SummaryEn = summaryEn;
        }

        public string SummaryText { get { return Loc.P(Summary, SummaryEn ?? Summary); } }
        public string WarningText { get { return Loc.P(Warning, string.IsNullOrEmpty(WarningEn) ? Warning : WarningEn); } }

        public Cmd R(Risk r) { Risk = r; return this; }
        public Cmd Fav() { Common = true; return this; }
        public Cmd Warn(string w, string en) { Warning = w; WarningEn = en; return this; }
        public Cmd A(params Arg[] args) { Args = args; return this; }
    }

    internal static class Registry
    {
        // 分類是**識別字**（InCategory 用它比對），顯示名稱查 CategoryLabel。
        internal const string CatCommon = "常用";
        internal const string CatSpawn = "生成";
        internal const string CatPlayer = "玩家";
        internal const string CatCombat = "戰鬥";
        internal const string CatBuy = "購買";
        internal const string CatItem = "物品";
        internal const string CatVehicle = "載具 / 賭場";
        internal const string CatProgress = "進度";

        internal static readonly string[] Categories =
        {
            CatCommon, CatSpawn, CatPlayer, CatCombat, CatBuy, CatItem, CatVehicle, CatProgress,
        };

        internal static string CategoryLabel(string category)
        {
            switch (category)
            {
                case CatCommon: return Loc.P(CatCommon, "Favourites");
                case CatSpawn: return Loc.P(CatSpawn, "Spawn");
                case CatPlayer: return Loc.P(CatPlayer, "Players");
                case CatCombat: return Loc.P(CatCombat, "Combat");
                case CatBuy: return Loc.P(CatBuy, "Buying");
                case CatItem: return Loc.P(CatItem, "Items");
                case CatVehicle: return Loc.P(CatVehicle, "Boat / Casino");
                case CatProgress: return Loc.P(CatProgress, "Progress");
                default: return category;
            }
        }

        // --- 參數建構捷徑 ---
        static Arg P(string label, string labelEn, bool all = false, bool none = false, string def = "me")
        {
            return new Arg(label, labelEn, ArgKind.Player) { AllowAll = all, AllowNone = none, Default = def };
        }
        static Arg I(string label, string labelEn, string def) { return new Arg(label, labelEn, ArgKind.Int) { Default = def }; }
        static Arg F(string label, string labelEn, string def) { return new Arg(label, labelEn, ArgKind.Float) { Default = def }; }
        static Arg B(string label, string labelEn, string def) { return new Arg(label, labelEn, ArgKind.Bool) { Default = def }; }
        static Arg Flag(string label, string labelEn, string token) { return new Arg(label, labelEn, ArgKind.Flag) { Token = token }; }
        static Arg T(string label, string labelEn, string hint, string hintEn) { return new Arg(label, labelEn, ArgKind.Text) { Hint = hint, HintEn = hintEn }; }
        static Arg Pick(string label, string labelEn, ArgKind kind, string def) { return new Arg(label, labelEn, kind) { Default = def }; }

        internal static readonly Cmd[] All =
        {
            // ---------------- 生成 ----------------
            new Cmd("spawn", CatSpawn, "在面前 2 公尺生成物品", "Spawn an item 2 m in front of you").Fav()
                .A(Pick("物品", "Item", ArgKind.Item, "tuna")),
            new Cmd("spawndead", CatSpawn, "生成一個已死亡的生物", "Spawn a creature that is already dead")
                .A(Pick("物品", "Item", ArgKind.Item, "tuna")),
            new Cmd("spawndrip", CatSpawn, "生成 drip（稀有變體）生物", "Spawn a drip (rare variant) creature")
                .A(Pick("生物", "Creature", ArgKind.Item, "tuna")),
            new Cmd("spawndripdead", CatSpawn, "生成已死亡的 drip 生物", "Spawn a dead drip creature")
                .A(Pick("生物", "Creature", ArgKind.Item, "tuna")),

            // ---------------- 玩家 ----------------
            new Cmd("godmode", CatPlayer, "切換無敵", "Toggle invulnerability").Fav(),
            new Cmd("hitplayer", CatPlayer, "對玩家造成傷害；繞過 PvP 時攻擊者為 null",
                    "Damage a player; the attacker is null when PvP is bypassed").R(Risk.Caution)
                .A(P("目標", "Target", all: true), I("傷害", "Damage", "999999"), B("繞過 PvP", "Bypass PvP", "true")),
            new Cmd("forcerespawn", CatPlayer, "強制重生", "Force a respawn").R(Risk.Caution)
                .A(P("目標", "Target", all: true)),
            new Cmd("forceresurrect", CatPlayer, "復活屍體", "Resurrect a body").R(Risk.Caution)
                .A(P("目標", "Target", all: true)),
            new Cmd("tpplayer", CatPlayer, "傳送玩家", "Teleport a player").R(Risk.Caution)
                .A(P("目標", "Target", all: true), new Arg("目的地", "Destination", ArgKind.PosOrVoid)),
            new Cmd("forceplayerpos", CatPlayer, "直接覆寫座標（不支援 all）",
                    "Overwrite coordinates directly (no all)").R(Risk.Caution)
                .A(P("目標", "Target"), new Arg("座標", "Position", ArgKind.Vec3) { Default = "0 50 0" }),
            new Cmd("forcedropall", CatPlayer, "讓玩家丟掉全部物品", "Make a player drop everything").R(Risk.Caution)
                .A(P("目標", "Target", all: true)),
            new Cmd("removeplayeritem", CatPlayer, "移除手上物品（不支援 all）",
                    "Remove the held item (no all)").R(Risk.Caution)
                .A(P("目標", "Target")),
            new Cmd("forcesetafk", CatPlayer, "標記 AFK（不支援 all）", "Flag a player as AFK (no all)").R(Risk.Caution)
                .A(P("目標", "Target"), B("AFK", "AFK", "true")),

            // ---------------- 戰鬥 ----------------
            new Cmd("hitcreature", CatCombat, "攻擊最近的生物；傷害填負數 = 治療",
                    "Hit the nearest creature; negative damage heals").Fav()
                .A(I("傷害", "Damage", "999999"), Flag("打全部（半徑 60m，上限 48）", "Hit all (60 m radius, max 48)", "all")),
            new Cmd("killboss", CatCombat, "對 Boss 造成 999999 傷害", "Deal 999999 damage to the boss").R(Risk.Caution).Fav(),
            new Cmd("oneshot", CatCombat, "切換一擊必殺（伺服器設定，影響全房）",
                    "Toggle one-shot kills (a server setting, affects the whole lobby)").R(Risk.Caution).Fav(),
            new Cmd("detonateall", CatCombat, "引爆場上所有炸藥", "Detonate every explosive in the scene").R(Risk.Caution)
                .Warn("封包量大，人多時可能被踢", "Heavy packet burst; may get you kicked in a busy lobby"),
            new Cmd("forcefinisheating", CatCombat, "吃掉最近的生物並回復", "Eat the nearest creature and recover")
                .A(P("目標", "Target", all: true)),
            new Cmd("killallcreatures", CatCombat, "把全部生物標記為已擊殺（圖鑑）",
                    "Mark every creature as killed (for the catalogue)"),
            new Cmd("killalldripcreatures", CatCombat, "同上，drip 版", "Same, for drip creatures"),
            new Cmd("resetallcreatures", CatCombat, "取消全部生物的擊殺標記", "Clear every creature kill mark"),
            new Cmd("resetalldripcreatures", CatCombat, "同上，drip 版", "Same, for drip creatures"),

            // ---------------- 購買 ----------------
            new Cmd("addmoney", CatBuy, "增加金錢", "Add money").Fav()
                .A(I("金額", "Amount", "9999")),
            new Cmd("removemoney", CatBuy, "減少金錢", "Remove money")
                .A(I("金額", "Amount", "9999")),
            new Cmd("buyfree", CatBuy, "免費取得物品", "Get an item for free").Fav()
                .A(Pick("物品", "Item", ArgKind.Item, "sniperrifle")),
            new Cmd("buybaitfree", CatBuy, "免費取得魚餌", "Get bait for free").R(Risk.Caution)
                .Warn("索引 0 會自踢，UI 已從 1 起跳", "Index 0 kicks you; the picker already starts at 1")
                .A(Pick("魚餌", "Bait", ArgKind.Bait, "1"), I("價格", "Price", "0")),
            new Cmd("buymotorfree", CatBuy, "免費升級船馬達（只能升不能降）",
                    "Upgrade the boat motor for free (upgrades only, no downgrades)")
                .A(Pick("馬達", "Motor", ArgKind.Motor, "1"), I("價格", "Price", "0")),
            new Cmd("buyradarfree", CatBuy, "免費取得船雷達", "Get the boat radar for free")
                .A(I("價格", "Price", "0")),
            new Cmd("forceunlockpocket", CatBuy, "解鎖背包口袋", "Unlock an inventory pocket").R(Risk.Caution)
                .Warn("索引 0 會自踢，UI 已從 1 起跳", "Index 0 kicks you; the picker already starts at 1")
                .A(Pick("槽位", "Slot", ArgKind.Pocket, "1")),
            new Cmd("forcebuyattachment", CatBuy, "為手上（或最近）的武器裝配件",
                    "Fit an attachment to the held (or nearest) weapon")
                .A(Pick("配件", "Attachment", ArgKind.Attachment, "0")),
            new Cmd("forcebuybulletupgrade", CatBuy, "子彈升級", "Bullet upgrade"),
            new Cmd("forcebuysharpnessupgrade", CatBuy, "近戰銳利度升級", "Melee sharpness upgrade"),
            new Cmd("takenpcitem", CatBuy, "從 NPC 手上拿走物品", "Take the item an NPC is holding").R(Risk.Caution)
                .Warn("不可填 255，會 KeyNotFoundException 自踢",
                      "Never pass 255 - it throws KeyNotFoundException and kicks you")
                .A(Pick("NPC", "NPC", ArgKind.Npc, "0")),

            // ---------------- 物品 ----------------
            new Cmd("setitemmultiplier", CatItem, "設定手上（或最近）物品的分數倍率",
                    "Set the score multiplier of the held (or nearest) item")
                .Warn("一個物品只能設一次：Item.SetKillscoreMultiplier 對倍率已非 1 的物品直接 return",
                      "One shot per item: Item.SetKillscoreMultiplier returns immediately if the multiplier is already not 1")
                .A(I("倍率", "Multiplier", "1000000")),
            new Cmd("setitemholder", CatItem, "把地上的物品塞進某人手裡",
                    "Put an item from the ground into someone's hands").R(Risk.Caution)
                .Warn("搶不走別人手上的東西——伺服器對「已有持有者」的物品會直接退回。留空則挑最近的無主物品",
                      "You cannot take an item out of someone else's hands - the server rejects any item that already has a holder. Leave the source empty to use the nearest unowned item")
                .A(P("新持有者", "New holder"), P("來源玩家", "Source player", def: "")),
            new Cmd("tpitems", CatItem, "把附近物品拉過來（上限 48）", "Pull nearby items to you (max 48)").R(Risk.Caution)
                .Warn("會先接管物理模擬權再搬；每個物品一個 RPC，一次太多可能被踢",
                      "Takes over physics simulation first, then moves them; one RPC per item, so too many at once may get you kicked")
                .A(new Arg("目的地", "Destination", ArgKind.Vec3) { Hint = "留空 = 你的位置", HintEn = "Empty = your position" }),
            new Cmd("hijackitemphysics", CatItem, "接管附近物品的物理模擬權（上限 48）",
                    "Take over physics simulation of nearby items (max 48)").R(Risk.Caution)
                .Warn("接管後物品由你這端模擬。單獨用看不出效果，通常搭配 /tpitems",
                      "Once taken over, those items simulate on your end. No visible effect on its own; usually paired with /tpitems"),
            new Cmd("grillhelditem", CatItem, "用岩漿烤手上的物品", "Grill the held item with lava"),
            new Cmd("grill", CatItem, "解鎖烤肉架", "Unlock the grill").Fav(),

            // ---------------- 載具 / 賭場 ----------------
            new Cmd("boat", CatVehicle, "解鎖船", "Unlock the boat").Fav(),
            new Cmd("setboatdriver", CatVehicle, "指定船的駕駛", "Set who is driving the boat").R(Risk.Caution)
                .A(P("駕駛", "Driver", none: true)),
            new Cmd("steerboat", CatVehicle, "蓋過駕駛的操舵輸入", "Override the driver's steering input").R(Risk.Caution)
                .Warn("船上沒有駕駛時伺服器整段忽略——先用 /setboatdriver。"
                    + "真駕駛每幀都在送輸入，所以要持續灌才看得出來",
                      "The server ignores this entirely while the boat has no driver - use /setboatdriver first. "
                    + "The real driver sends input every frame, so you have to keep feeding it to see anything")
                .A(F("轉向 X", "Steer X", "0"), F("油門 Y", "Throttle Y", "1"), F("持續秒數", "Duration (s)", "3")),
            new Cmd("forceplacebet", CatVehicle, "強制以指定顏色開一局輪盤",
                    "Start a roulette round with a chosen colour").R(Risk.Caution)
                .Warn("選的是「你押哪個顏色」，不是開獎結果——開獎仍然是隨機的。"
                    + "需要桌上已放下注物品、且沒有正在轉。只能在賭場島用",
                      "This picks the colour you bet on, not the winning one - the draw is still random. "
                    + "Needs an item already on the table and no spin in progress. Casino island only")
                .A(Pick("顏色", "Colour", ArgKind.Roulette, "0")),
            new Cmd("spoofroulette", CatVehicle, "竄改輪盤的顯示角度（純視覺）",
                    "Fake the roulette display angle (visual only)").R(Risk.Caution)
                .Warn("只改別人畫面上輪盤的擺放，不影響結果也不影響賠付。只能在賭場島用",
                      "Only moves the wheel object on other screens; it changes neither the result nor the payout. Casino island only")
                .A(F("角度", "Angle", "180")),
            new Cmd("slots", CatVehicle, "設定拉霸作弊外觀", "Set the slot machine cheat visuals")
                .A(T("物品名", "Item name", "非物品字串則指向船", "A non-item string targets the boat"),
                   I("外觀索引", "Skin index", "0")),

            // ---------------- 進度 ----------------
            new Cmd("nextisland", CatProgress, "前往下一座島", "Go to the next island").R(Risk.Caution).Fav(),
            new Cmd("previsland", CatProgress, "前往上一座島", "Go to the previous island").R(Risk.Caution),
            new Cmd("allskins", CatProgress, "解鎖全部外觀", "Unlock every skin"),
            new Cmd("noskins", CatProgress, "鎖上全部外觀", "Lock every skin"),
            new Cmd("showkillscores", CatProgress, "顯示全部擊殺分數獎勵", "Show every kill score reward"),
            new Cmd("unlockachievements", CatProgress, "解鎖全部 Steam 成就", "Unlock every Steam achievement").R(Risk.Danger)
                .Warn("直接寫入你的 Steam 帳號，不可逆", "Writes straight to your Steam account. Not reversible"),
            new Cmd("lockachievements", CatProgress, "重置全部 Steam 成就", "Reset every Steam achievement").R(Risk.Danger)
                .Warn("直接寫入你的 Steam 帳號，不可逆", "Writes straight to your Steam account. Not reversible"),
            new Cmd("finishgame", CatProgress, "結束整場遊戲，跑製作人員名單",
                    "End the whole run and roll the credits").R(Risk.Danger)
                .Warn("會結束所有人的遊戲", "Ends the game for everyone")
                .A(Flag("我確定", "I am sure", "confirm")),
            new Cmd("sendfinishgame", CatProgress, "透過 RPC 結束遊戲（只在第 5 島有效）",
                    "End the game over RPC (island 5 only)").R(Risk.Danger)
                .Warn("會結束所有人的遊戲", "Ends the game for everyone")
                .A(Flag("我確定", "I am sure", "confirm")),
            new Cmd("spoofprojectile", CatProgress, "偽造彈丸來源（最多 64 發）",
                    "Fake the owner of projectiles (max 64)").R(Risk.Danger)
                .Warn("超過 64 會超過 MTU 被踢", "More than 64 exceeds the MTU and kicks you")
                .A(P("歸屬玩家", "Credited player"), I("數量", "Count", "1")),
            new Cmd("spoofprojectilehit", CatProgress, "偽造彈丸命中歸屬",
                    "Fake who a projectile hit is credited to").R(Risk.Danger)
                .A(I("彈丸 ID", "Projectile ID", "0"), P("歸屬玩家", "Credited player")),
        };

        internal static List<Cmd> InCategory(string category)
        {
            var list = new List<Cmd>();
            foreach (var c in All)
            {
                if (category == CatCommon ? c.Common : c.Category == category) list.Add(c);
            }
            return list;
        }
    }
}

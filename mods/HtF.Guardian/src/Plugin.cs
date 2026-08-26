using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;
using UnityEngine;

namespace HtF.Guardian
{
    /// <summary>違規次數超過上限時要做什麼。</summary>
    internal enum OnLimit { 只記錄, 踢出, 踢出並封鎖 }

    /// <summary>
    /// 房主端的 ServerRpc 驗證層。
    ///
    /// 遊戲是 host-authoritative（listen server），而 <c>Server.cs</c> 裡有
    /// 56 個 <c>[ServerRpc(RequireOwnership = false)]</c>。FishNet 會替每個
    /// ServerRpc 的 reader 注入**真實的發送端連線**，但除了 <c>SpawnPlayer</c>
    /// 以外，沒有任何一個 <c>RpcLogic___*</c> 用到它——它們完全相信客戶端在
    /// 參數裡自填的 <c>Player</c> / <c>SteamID</c> / <c>cost</c> 來決定
    /// 「誰在操作、對誰操作、花多少錢」。
    ///
    /// 這個 mod 把那條被丟掉的資訊接回來：
    ///
    /// 1. 對每個 <c>RpcReader___*</c> 下 prefix，把 FishNet 給的 <c>conn</c>
    ///    存進 <see cref="Sender"/>（reader 會在同一個呼叫堆疊裡直接叫 logic，
    ///    所以一個靜態欄位就夠，見 <see cref="Sender"/> 的說明）。
    /// 2. 對每個危險的 <c>RpcLogic___*</c> 下 prefix，用那個 <c>conn</c> 驗證
    ///    參數；不合就 <c>return false</c> 擋掉，或把參數改成正確的值。
    /// 3. 每個連線每個 RPC 各有一個速率桶，擋洪水攻擊。
    /// 4. 違規累積到上限可以自動踢出／封鎖。
    ///
    /// **只有房主要裝**：所有 <c>RpcLogic___*</c> 都只在伺服器端跑
    /// （reader 開頭就是 <c>if (!base.IsServerInitialized) return;</c>），
    /// 裝在純客戶端上不會有任何作用，也不會有壞處。
    ///
    /// 做不到的事：Harmony 補不上 SyncVar 或 ServerRpc（FishNet 靠編譯期
    /// IL weaving），所以這裡不建立任何新的同步狀態，只在既有的伺服器端
    /// 進入點上加守衛。
    /// </summary>
    [BepInPlugin(Guid, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.guardian";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // 一般
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> AlsoCheckHost;

        // 驗證
        internal static ConfigEntry<bool> CheckIdentity, CheckPrices, CheckValues, CheckIndices, CheckRates;
        internal static ConfigEntry<float> RateMultiplier;
        internal static ConfigEntry<bool> FinishGameHostOnly;
        internal static ConfigEntry<bool> CheckSpeed;
        internal static ConfigEntry<float> MaxSpeed;

        // 上限
        internal static ConfigEntry<int> MaxPlayerDamage, MaxSourcelessDamage, MaxCreatureDamage;
        internal static ConfigEntry<float> MaxScoreMultiplier;
        internal static ConfigEntry<int> MaxProjectilesPerShot, MaxChatLength;

        // 處置
        internal static ConfigEntry<int> ViolationLimit;
        internal static ConfigEntry<float> ViolationDecay;
        internal static ConfigEntry<OnLimit> Action;
        internal static ConfigEntry<float> LogCooldown;
        internal static ConfigEntry<bool> AnnounceInChat;

        // 介面
        internal static ConfigEntry<KeyboardShortcut> PanelKey;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<string> FontName;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 和列舉成員名一律維持原本的中文：那些是 .cfg 的識別字，
            // 翻譯它們會讓使用者既有的值全部失效。
            Loc.Section("一般", "General");
            Loc.Section("驗證", "Validation");
            Loc.Section("上限", "Limits");
            Loc.Section("處置", "Enforcement");
            Loc.Section("介面", "Interface");

            Loc.EnumValue(OnLimit.只記錄, "只記錄", "Log Only");
            Loc.EnumValue(OnLimit.踢出, "踢出", "Kick");
            Loc.EnumValue(OnLimit.踢出並封鎖, "踢出並封鎖", "Kick And Ban");

            Enabled = Loc.Bind(Config, "一般", "啟用", true, "Enabled",
                "總開關。關掉之後所有守衛都直接放行，patch 仍在但不做事。",
                "Master switch. While off every guard passes straight through; the patches stay in place but do nothing.");
            AlsoCheckHost = Loc.Bind(Config, "一般", "也檢查房主自己", false, "Also Check The Host",
                "預設不檢查房主：房主的連線就是伺服器本身，遊戲有不少邏輯"
                + "（爆炸傷害、Boss 攻擊）本來就是在伺服器端代所有人送出的，檢查它會誤判。",
                "Off by default. The host's connection is the server itself, and several of the game's own systems "
                + "(explosion damage, boss attacks) legitimately send RPCs on everyone's behalf from there.");

            CheckIdentity = Loc.Bind(Config, "驗證", "檢查操作者身分", true, "Check Actor Identity",
                "RPC 參數裡的 Player／物品持有者，必須就是送出這個封包的那條連線。"
                + "這一條擋掉絕大多數的搗亂：強制移動別人、清空別人背包、冒名發言、替別人下注。",
                "The Player (or item holder) named in the RPC must be the connection that actually sent the packet. "
                + "This one check stops most griefing: moving other players, emptying their inventories, "
                + "impersonating them in chat, betting on their behalf.");
            CheckPrices = Loc.Bind(Config, "驗證", "檢查購買價格", true, "Check Purchase Prices",
                "魚餌、船馬達、船雷達的價格由客戶端在 RPC 裡自填。打開後改成拿場上"
                + "販賣點的實際售價比對，對不上就用正確價格扣款。",
                "Bait, boat motor and boat radar prices are filled in by the client. When on, they are checked "
                + "against the actual price on the shop object in the scene and corrected if they do not match.");
            CheckValues = Loc.Bind(Config, "驗證", "檢查數值範圍", true, "Check Value Ranges",
                "傷害、分數倍率、復活進度、無線電頻率等由客戶端指定的數字必須落在合理範圍。",
                "Damage, score multipliers, revive progress, radio frequency and other client-supplied numbers "
                + "must be inside a sane range.");
            CheckIndices = Loc.Bind(Config, "驗證", "檢查索引範圍", true, "Check Index Ranges",
                "擋掉會讓伺服器端丟例外的索引（魚餌 0、口袋 0、NPC 255）。"
                + "FishNet 會把「RPC 執行時丟例外」當成惡意封包，直接踢掉發送者——"
                + "擋在前面比較乾淨，log 也不會被洗版。",
                "Reject indices that make the server throw (bait 0, pocket 0, NPC 255). FishNet treats an exception "
                + "during RPC execution as malformed data and kicks the sender, so it is cleaner to stop them here.");
            CheckRates = Loc.Bind(Config, "驗證", "速率限制", true, "Rate Limiting",
                "每條連線的每個 RPC 各有一個速率桶，超過就丟棄。",
                "Every RPC gets its own token bucket per connection; anything over the rate is dropped.");
            RateMultiplier = Loc.Bind(Config, "驗證", "速率上限倍率", 1.0f, "Rate Limit Multiplier",
                "所有速率上限一起乘上這個值。網路不穩、玩家很多時可以調高。",
                "Scales every rate limit at once. Raise it if the connection is jittery or the lobby is large.",
                new AcceptableValueRange<float>(0.25f, 10f));
            FinishGameHostOnly = Loc.Bind(Config, "驗證", "結束遊戲限房主", false, "Only The Host May End The Run",
                "遊戲原本任何人都能送「結束遊戲」。打開後只有房主能結束。",
                "Vanilla lets anyone send the end-of-run RPC. When on, only the host can.");
            CheckSpeed = Loc.Bind(Config, "驗證", "移動速度檢查", false, "Movement Speed Check",
                "預設關閉：位置更新是 unreliable 封包，掉包、傳送門、船上移動都可能造成誤判。"
                + "打開後超過下面那個速度的位置更新會被丟棄（不會踢人）。",
                "Off by default: position updates ride an unreliable channel, and packet loss, teleports and boat "
                + "rides all produce false positives. When on, updates faster than the speed below are dropped (nobody is kicked).");
            MaxSpeed = Loc.Bind(Config, "驗證", "最高移動速度", 30f, "Maximum Movement Speed",
                "公尺／秒。玩家跑步大約 6，船上會更快。",
                "Metres per second. A sprinting player is around 6; riding the boat is faster.",
                new AcceptableValueRange<float>(5f, 200f));

            MaxPlayerDamage = Loc.Bind(Config, "上限", "玩家傷害上限", 500, "Maximum Damage To A Player",
                "單次對玩家的傷害。玩家血量上限是 100，所以 500 已經很寬鬆了；"
                + "房主有開「一擊必殺」的話要調到 99999 以上。",
                "Per-hit damage against a player. Player health caps at 100, so 500 is already generous. "
                + "Raise it above 99999 if you run with one-shot kills enabled.",
                new AcceptableValueRange<int>(1, 1000000));
            MaxSourcelessDamage = Loc.Bind(Config, "上限", "無來源傷害上限", 500, "Maximum Sourceless Damage",
                "沒有指定攻擊者的玩家傷害（溺水、生物撞擊、Boss 攻擊）。這種封包沒辦法驗證來源"
                + "——遊戲本身就允許任何客戶端代生物送出——所以只能夾上限加速率限制。"
                + "不要調太低：Boss 傷害會隨人數成長（BossManager.GetBossDamage 每多一人 +20%）。",
                "Player damage with no attacker (drowning, creature collisions, boss attacks). The game itself lets any "
                + "client send these on a creature's behalf, so there is nothing to verify; all we can do is cap and rate-limit. "
                + "Do not set it too low: boss damage scales with the player count (BossManager.GetBossDamage adds 20% per extra player).",
                new AcceptableValueRange<int>(1, 1000000));
            MaxCreatureDamage = Loc.Bind(Config, "上限", "生物傷害上限", 100000, "Maximum Damage To A Creature",
                "單次對生物的傷害。負值一律擋掉——那會替生物回血。"
                + "預設值容得下遊戲自己的「一擊必殺」（99999）。",
                "Per-hit damage against a creature. Negative values are always rejected: they heal it instead. "
                + "The default leaves room for the game's own one-shot value (99999).",
                new AcceptableValueRange<int>(1, 100000000));
            MaxScoreMultiplier = Loc.Bind(Config, "上限", "分數倍率上限", 100f, "Maximum Score Multiplier",
                "擊殺分數倍率。遊戲自己算出來的值遠低於這個數。",
                "Kill-score multiplier. The values the game computes itself are far below this.",
                new AcceptableValueRange<float>(1f, 100000f));
            MaxProjectilesPerShot = Loc.Bind(Config, "上限", "單次彈丸數上限", 32, "Maximum Projectiles Per Shot",
                "一次 AddProjectiles 最多幾發。霰彈槍大約 8 發。",
                "How many projectiles one AddProjectiles may carry. A shotgun fires about 8.",
                new AcceptableValueRange<int>(1, 512));
            MaxChatLength = Loc.Bind(Config, "上限", "聊天字數上限", 500, "Maximum Chat Length",
                "超過就截掉。",
                "Longer messages are truncated.",
                new AcceptableValueRange<int>(16, 4000));

            ViolationLimit = Loc.Bind(Config, "處置", "違規上限", 40, "Violation Limit",
                "同一條連線累積到這個次數就執行下面的處置。0 = 不處置。"
                + "數字要留餘裕：網路延遲本來就會製造零星的假違規。",
                "Run the action below once one connection reaches this many violations. 0 disables it. "
                + "Leave headroom: latency alone produces the occasional false positive.",
                new AcceptableValueRange<int>(0, 10000));
            ViolationDecay = Loc.Bind(Config, "處置", "違規衰減秒數", 60f, "Violation Decay",
                "乾淨玩這麼多秒就消掉一次違規。0 = 不衰減（計數變成「這輩子累計」）。"
                + "有衰減，上面那個上限的意思才是「短時間內密集違規」——"
                + "延遲造成的零星假違規會自己被吸收掉，不會累積幾小時之後把正常玩的人踢掉。",
                "Forgive one violation for every this many seconds of clean play. 0 disables decay "
                + "(the counter becomes a lifetime total). With decay the limit above means \"a burst of violations\" "
                + "rather than a slow accumulation, so the occasional latency-induced false positive is absorbed "
                + "instead of eventually kicking an innocent player.",
                new AcceptableValueRange<float>(0f, 3600f));

            Action = Loc.Bind(Config, "處置", "超過時", OnLimit.只記錄, "Action",
                "預設只記錄。先觀察一陣子面板上的數字，再決定要不要自動踢人。",
                "Log only by default. Watch the panel for a while before letting it kick people automatically.");
            LogCooldown = Loc.Bind(Config, "處置", "通報冷卻秒數", 2f, "Report Cooldown",
                "同一條連線、同一個 RPC 在這段時間內只寫一次 log（違規次數照樣累加）。",
                "Only write one log line per connection per RPC within this window. The counter still goes up.",
                new AcceptableValueRange<float>(0f, 60f));
            AnnounceInChat = Loc.Bind(Config, "處置", "在聊天視窗提示", true, "Announce In Chat",
                "踢出／封鎖時在房主自己的聊天視窗留一行（只有房主看得到）。",
                "Leave a line in the host's own chat window when someone is kicked or banned (host-side only).");

            PanelKey = Loc.Bind(Config, "介面", "面板按鍵", new KeyboardShortcut(KeyCode.F11), "Panel Key",
                "開關監控面板的按鍵。面板只在你是房主時有內容。",
                "Key that opens the monitor panel. It only has anything in it while you are the host.");
            UiScale = Loc.Bind(Config, "介面", "縮放", 1.0f, "UI Scale",
                "面板縮放倍率。",
                "Scale factor of the panel.",
                new AcceptableValueRange<float>(0.6f, 2.0f));
            FontName = Loc.Bind(Config, "介面", "字型", "Microsoft JhengHei UI", "Font",
                "面板用的系統字型名稱。留空 = 用 Unity 內建字型（中文可能變方塊）。",
                "System font name used by the panel. Leave empty to use the built-in Unity font (CJK text may render as boxes).");

            Bans.Load();

            _harmony = new Harmony(Guid);
            Patcher.Apply(_harmony);

            Log.LogInfo("Guardian 已載入，按 " + PanelKey.Value + " 開啟面板。");
        }

        private void OnDestroy()
        {
            Watcher.Detach();
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            // 語言跟著 ConfigMenu 的設定（沒裝就跟著遊戲語系），每幀重判一次。
            // OnGUI 在 Update 之後跑，所以同一幀畫出來的文字語言一致。
            Loc.Resolve();

            Watcher.Tick();
            if (PanelKey.Value.IsDown()) Panel.Toggle();
            Panel.Tick();
        }

        private void OnGUI()
        {
            Panel.Draw();
        }
    }
}

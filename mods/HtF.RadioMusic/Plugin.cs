using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HtF.Shared;
using UnityEngine;

namespace HtF.RadioMusic
{
    /// <summary>
    /// 收音機自訂音樂。
    ///
    /// 純客戶端：頻率是 SyncVar 會同步（別人看得到你在轉台），但音檔是本地的，
    /// 所以換成自訂音樂後只有你自己聽得到，不會影響別人，也不需要別人裝。
    ///
    /// 「同步播放」是這條規則的例外，但仍然不送封包：曲目與位置都從 FishNet 的
    /// 網路時間算出來，同樣的檔案在每台機器上會得到同樣的答案。見 <see cref="Sync"/>。
    ///
    /// 順帶把那個「沙沙聲」做成可調。它是刻意的——Radio.ApplyVolume 最後一行
    ///     _noiseSource.volume = (1f - 最接近頻道的準度) * 0.075f;
    /// 在模擬 FM 空頻雜訊：調準了是 0，偏掉才變大。
    /// </summary>
    [BepInPlugin(Guid, "HtF Radio Music", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "htf.radiomusic";

        internal static ManualLogSource Log;

        internal static ConfigEntry<float> NoiseVolume, MusicVolume;
        internal static ConfigEntry<bool> PlayThroughBoss, AutoAdvance, SyncPlayback;
        internal static ConfigEntry<KeyboardShortcut> NextTrackKey, ReloadKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Loc.Bind = 綁設定 + 登記英文名稱與中英說明（見 mods/Shared/Loc.cs）。
            // key 一律維持原本的中文：那是 .cfg 的識別字，翻譯它會讓舊設定全部失效。
            Loc.Section("聲音", "Audio");
            Loc.Section("按鍵", "Keys");

            NoiseVolume = Loc.Bind(Config, "聲音", "雜訊音量倍率", 1.0f, "Static Noise Volume",
                "收音機沒調準時的「沙沙」聲。0 = 完全關掉。\n"
                + "這個雜訊是遊戲刻意做的：Radio.ApplyVolume 依「最接近頻道的準度」算出音量，\n"
                + "誤差 0.5 以內完全沒有雜訊，超過 1.5 就是全雜訊。",
                "The hiss you hear when the radio is off-station. 0 = silence it completely.\n"
                + "That hiss is deliberate: Radio.ApplyVolume derives it from how close you are to the nearest station,\n"
                + "so within 0.5 there is no noise at all, and past 1.5 it is pure noise.",
                new AcceptableValueRange<float>(0f, 3f));

            MusicVolume = Loc.Bind(Config, "聲音", "音樂音量倍率", 1.0f, "Music Volume",
                "頻道音量。遊戲原本的基準是 0.3。",
                "Station volume. The vanilla baseline is 0.3.",
                new AcceptableValueRange<float>(0f, 5f));

            PlayThroughBoss = Loc.Bind(Config, "聲音", "Boss 出現時繼續播放", false, "Keep Playing During Boss",
                "遊戲預設在 Boss 登場時把收音機整個靜音（頻道和雜訊都歸零）。\n"
                + "打開這個選項會把音量重新算回來，讓音樂繼續。",
                "By default the game mutes the whole radio when a boss shows up (both stations and noise drop to zero).\n"
                + "Turn this on to recompute the volume and keep the music going.");

            AutoAdvance = Loc.Bind(Config, "聲音", "自動接下一首", true, "Auto-Advance Playlist",
                "一個頻道分到多首歌時，像真的電台一樣自己接著播下去。\n"
                + "關掉的話一個頻道同時只會有一首，其餘要按「下一首」熱鍵才聽得到"
                + "（這是舊版的行為）。\n"
                + "歌曲本來就是輪流平均分給各頻道的；打開這個之後，"
                + "多出來的歌不會再被藏在熱鍵後面。",
                "When a station has more than one track, play them back to back like a real radio station.\n"
                + "Turn it off and each station only ever holds one track, with the rest reachable only "
                + "through the Next Track hotkey (the old behaviour).\n"
                + "Tracks are spread evenly across the stations either way; this just stops the extra ones "
                + "from hiding behind a hotkey.");

            SyncPlayback = Loc.Bind(Config, "聲音", "同步播放", false, "Listen Together",
                "讓同一個房間的人聽到同一首、同一個位置。\n"
                + "前提：每個人都要裝這個 mod，而且音樂資料夾的內容要一模一樣"
                + "——mod 沒辦法替你發音檔，這一點只能自己約好。\n"
                + "原理：頻道的曲目串成一條連續的時間軸（像電台節目表），"
                + "由 FishNet 的網路時間決定現在播到哪裡。純計算，不送任何封包。\n"
                + "這是「自動接下一首」的延伸：節目表本身用本機時鐘就成立，"
                + "打開這個只是把時鐘換成網路時間，讓所有人的節目表對齊。\n"
                + "打開後「下一首」會失效——跳過是本機動作，沒辦法讓其他人跟著跳。",
                "Make everyone in the lobby hear the same track at the same position.\n"
                + "Requires everyone to install this mod and have identical music folders "
                + "— the mod cannot hand out audio files, so that part is up to you.\n"
                + "How it works: each station's tracks form one continuous timeline (like a real station's schedule), "
                + "and FishNet's network time decides where playback currently is. Pure computation, no packets.\n"
                + "This builds on Auto-Advance: the schedule itself works off a local clock, and turning this on "
                + "simply swaps that clock for network time so everyone's schedule lines up.\n"
                + "While this is on, Next Track does nothing: skipping is a local action and cannot be shared.");

            NextTrackKey = Loc.Bind(Config, "按鍵", "下一首", new KeyboardShortcut(KeyCode.F7), "Next Track",
                "跳過目前這首。「自動接下一首」開著時是把節目表推到下一首，"
                + "關著時是切換這個頻道當前的曲目。",
                "Skip the current track. With Auto-Advance on it pushes the schedule to the next track; "
                + "with it off it switches which track the station is holding.");

            ReloadKey = Loc.Bind(Config, "按鍵", "重新載入音樂", new KeyboardShortcut(KeyCode.F8), "Reload Music",
                "不用重開遊戲就能套用資料夾裡新增的檔案。",
                "Pick up files you just added to the folder without restarting the game.");

            MusicLibrary.EnsureFolder();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(RadioPatcher));
            RadioPatcher.VerifyTargets();

            StartCoroutine(LoadThenApply());

            Log.LogInfo("Radio Music 已載入。音樂資料夾：" + MusicLibrary.Folder);
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            if (NextTrackKey.Value.IsDown())
            {
                if (SyncPlayback.Value)
                {
                    // 同步模式的節目表是從網路時間算出來的，本機的跳過偏移根本沒被讀。
                    // 切一首只會讓自己脫隊，所以直接擋掉並說清楚為什麼。
                    Log.LogInfo("「同步播放」開著時不能切歌——跳過是本機動作，沒辦法讓其他人跟著跳。");
                }
                else if (AutoAdvance.Value)
                {
                    RadioPatcher.SkipCurrent();
                    Log.LogInfo("已跳過這首。");
                }
                else
                {
                    MusicLibrary.AdvanceAll();
                    RadioPatcher.ApplyToAll();
                    Log.LogInfo("已切到下一首。");
                }
            }
            if (ReloadKey.Value.IsDown())
            {
                StopAllCoroutines();
                StartCoroutine(LoadThenApply());
            }

            // 節目表要每幀維持：一首放完要接下一首，而 ApplyVolume 只在
            // 轉台和 Boss 事件時才被呼叫，接不了。
            RadioPatcher.TickPlaylist();
        }

        private System.Collections.IEnumerator LoadThenApply()
        {
            yield return MusicLibrary.LoadAll();

            // 曲目換了，上一輪「跳過」累積的偏移沒有意義。
            Sync.ClearSkips();

            if (MusicLibrary.TotalClips == 0)
                Log.LogInfo("音樂資料夾裡沒有可用的檔案，收音機維持原曲。");

            // 音樂是非同步載入的，載完時場上可能已經有收音機了，回頭補套用。
            RadioPatcher.ApplyToAll();

            // 舊 clip 的記憶體要等這時候才放：ApplyToAll 之前砍的話，
            // AudioSource 上掛的會是已經被銷毀的 clip。
            MusicLibrary.DisposeStale(RadioPatcher.IsClipInUse);
        }
    }
}

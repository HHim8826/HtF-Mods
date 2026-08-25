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
        internal static ConfigEntry<bool> PlayThroughBoss;
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

            NextTrackKey = Loc.Bind(Config, "按鍵", "下一首", new KeyboardShortcut(KeyCode.F7), "Next Track",
                "同一個頻道放多首時，切到下一首。",
                "Skip to the next track when a station has more than one.");

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
                MusicLibrary.AdvanceAll();
                RadioPatcher.ApplyToAll();
                Log.LogInfo("已切到下一首。");
            }
            if (ReloadKey.Value.IsDown())
            {
                StopAllCoroutines();
                StartCoroutine(LoadThenApply());
            }
        }

        private System.Collections.IEnumerator LoadThenApply()
        {
            yield return MusicLibrary.LoadAll();

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

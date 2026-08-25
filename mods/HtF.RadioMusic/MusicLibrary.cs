using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HtF.RadioMusic
{
    /// <summary>
    /// 掃描音樂資料夾並載入成 AudioClip。
    ///
    /// 資料夾規則：
    ///   music/1/  music/2/ ...  以頻道編號命名的子資料夾 → 指定給該頻道
    ///   music/*.ogg           → 沒被子資料夾指定的頻道，依序輪流分配
    /// </summary>
    internal static class MusicLibrary
    {
        internal static bool Ready { get; private set; }

        // 頻道索引 -> 該頻道可用的曲目
        private static readonly Dictionary<int, List<AudioClip>> ByChannel = new Dictionary<int, List<AudioClip>>();
        private static readonly List<AudioClip> Shared = new List<AudioClip>();

        // 每個頻道目前播到第幾首
        private static readonly Dictionary<int, int> Cursor = new Dictionary<int, int>();

        internal static string Folder
        {
            get { return Path.Combine(BepInEx.Paths.ConfigPath, "HtF.RadioMusic"); }
        }

        internal static int TotalClips
        {
            get
            {
                int n = Shared.Count;
                foreach (KeyValuePair<int, List<AudioClip>> kv in ByChannel) n += kv.Value.Count;
                return n;
            }
        }

        /// <summary>第一次執行時把資料夾和說明檔建好，使用者才知道要把檔案放哪。</summary>
        internal static void EnsureFolder()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string readme = Path.Combine(Folder, "把音樂放這裡.txt");
                if (!File.Exists(readme))
                {
                    // 這個檔案寫一次就放著，沒辦法跟著語言重畫，所以中英併列。
                    File.WriteAllText(readme,
                        "把 .ogg / .wav / .mp3 放進這個資料夾。\r\n" +
                        "\r\n" +
                        "分配方式：\r\n" +
                        "  直接放在這裡      -> 依序輪流分配給各個頻道\r\n" +
                        "  放進子資料夾 1\\   -> 指定給第 1 個頻道\r\n" +
                        "  放進子資料夾 2\\   -> 指定給第 2 個頻道，依此類推\r\n" +
                        "\r\n" +
                        "一個頻道放多首的話，遊戲中按「下一首」熱鍵（預設 F7）可以切換。\r\n" +
                        "\r\n" +
                        "格式建議用 .ogg——Unity 執行期解碼最穩。\r\n" +
                        ".mp3 不一定所有環境都能載，載不動會在 BepInEx log 留訊息。\r\n" +
                        "\r\n" +
                        "注意：換成自訂音樂後只有你自己聽得到，其他玩家聽到的還是原曲。\r\n" +
                        "（頻率是同步的，音檔是本地的。）\r\n" +
                        "\r\n" +
                        "------------------------------------------------------------\r\n" +
                        "\r\n" +
                        "Drop .ogg / .wav / .mp3 files into this folder.\r\n" +
                        "\r\n" +
                        "How they are assigned:\r\n" +
                        "  loose in this folder  -> handed out to the stations in turn\r\n" +
                        "  in subfolder 1\\       -> assigned to station 1\r\n" +
                        "  in subfolder 2\\       -> assigned to station 2, and so on\r\n" +
                        "\r\n" +
                        "If a station has several tracks, the Next Track hotkey (F7 by default) cycles them.\r\n" +
                        "\r\n" +
                        ".ogg is the safest format - Unity decodes it most reliably at runtime.\r\n" +
                        ".mp3 does not load everywhere; failures are reported in the BepInEx log.\r\n" +
                        "\r\n" +
                        "Note: custom music is local to you. Everyone else still hears the original tracks.\r\n" +
                        "(The frequency is synced, the audio files are not.)\r\n");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("建立音樂資料夾失敗：" + e.Message); }
        }

        internal static IEnumerator LoadAll()
        {
            Ready = false;
            ByChannel.Clear();
            Shared.Clear();
            Cursor.Clear();

            if (!Directory.Exists(Folder))
            {
                Ready = true;
                yield break;
            }

            // 子資料夾：名稱是數字的就當成頻道編號（1 起算）
            string[] dirs;
            try { dirs = Directory.GetDirectories(Folder); }
            catch (Exception) { dirs = new string[0]; }

            for (int i = 0; i < dirs.Length; i++)
            {
                int channel;
                if (!int.TryParse(Path.GetFileName(dirs[i]).Trim(), out channel)) continue;
                channel -= 1; // 使用者寫 1 起算，內部 0 起算
                if (channel < 0) continue;

                var list = new List<AudioClip>();
                foreach (string file in AudioFiles(dirs[i]))
                {
                    AudioClip clip = null;
                    yield return Load(file, c => clip = c);
                    if (clip) list.Add(clip);
                }
                if (list.Count > 0) ByChannel[channel] = list;
            }

            foreach (string file in AudioFiles(Folder))
            {
                AudioClip clip = null;
                yield return Load(file, c => clip = c);
                if (clip) Shared.Add(clip);
            }

            Ready = true;
            Plugin.Log.LogInfo("音樂載入完成：共 " + TotalClips + " 首（指定頻道 " + ByChannel.Count + " 個）。");
        }

        private static IEnumerable<string> AudioFiles(string dir)
        {
            string[] all;
            try { all = Directory.GetFiles(dir); }
            catch (Exception) { yield break; }

            Array.Sort(all, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < all.Length; i++)
            {
                string ext = Path.GetExtension(all[i]).ToLowerInvariant();
                if (ext == ".ogg" || ext == ".wav" || ext == ".mp3") yield return all[i];
            }
        }

        private static IEnumerator Load(string path, Action<AudioClip> onDone)
        {
            AudioType type;
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ogg": type = AudioType.OGGVORBIS; break;
                case ".wav": type = AudioType.WAV; break;
                default: type = AudioType.MPEG; break;
            }

            string url = "file:///" + path.Replace("\\", "/").Replace("#", "%23").Replace("?", "%3F");
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                // 不用串流：遊戲在解除靜音時會做 _channelSource.time = ... 的定位，
                // 串流 clip 的 seek 行為不可靠。
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning("載不動 " + Path.GetFileName(path) + "：" + req.error);
                    onDone(null);
                    yield break;
                }

                AudioClip clip;
                try { clip = DownloadHandlerAudioClip.GetContent(req); }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("解碼失敗 " + Path.GetFileName(path) + "：" + e.Message);
                    onDone(null);
                    yield break;
                }

                if (!clip || clip.length <= 0.01f)
                {
                    Plugin.Log.LogWarning("略過 " + Path.GetFileName(path) + "（長度為 0 或解碼不完整）");
                    onDone(null);
                    yield break;
                }

                clip.name = Path.GetFileNameWithoutExtension(path);
                onDone(clip);
            }
        }

        /// <summary>取得某頻道目前該播的曲目；沒有可用曲目時回傳 null（代表保留原曲）。</summary>
        internal static AudioClip ClipFor(int channelIndex, int totalChannels)
        {
            List<AudioClip> list = TracksFor(channelIndex, totalChannels);
            if (list == null || list.Count == 0) return null;

            int cur;
            if (!Cursor.TryGetValue(channelIndex, out cur)) cur = 0;
            return list[((cur % list.Count) + list.Count) % list.Count];
        }

        internal static void Advance(int channelIndex)
        {
            int cur;
            Cursor.TryGetValue(channelIndex, out cur);
            Cursor[channelIndex] = cur + 1;
        }

        internal static void AdvanceAll()
        {
            var keys = new List<int>(Cursor.Keys);
            for (int i = 0; i < keys.Count; i++) Cursor[keys[i]] = Cursor[keys[i]] + 1;
            // 還沒有游標的頻道也要推進
            for (int c = 0; c < 16; c++) if (!Cursor.ContainsKey(c)) Cursor[c] = 1;
        }

        private static List<AudioClip> TracksFor(int channelIndex, int totalChannels)
        {
            List<AudioClip> explicitList;
            if (ByChannel.TryGetValue(channelIndex, out explicitList) && explicitList.Count > 0)
                return explicitList;

            if (Shared.Count == 0 || totalChannels <= 0) return null;

            // 共用曲目輪流分給沒有指定資料夾的頻道
            var mine = new List<AudioClip>();
            for (int i = channelIndex; i < Shared.Count; i += totalChannels) mine.Add(Shared[i]);
            return mine.Count > 0 ? mine : null;
        }
    }
}

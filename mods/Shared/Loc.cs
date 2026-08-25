using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace HtF.Shared
{
    /// <summary>語言選項。<see cref="Language.Auto"/> = 跟著遊戲目前的語系走。</summary>
    public enum Language { Auto, Chinese, English }

    /// <summary>
    /// 中英雙語的共用底層。這個檔案由 `mods/Common.props` **編譯進每一個 mod**
    /// （跟其他共用設定一樣是編譯期共用，不是執行期相依），所以每個 DLL 裡都有
    /// 自己的一份 `HtF.Shared.Loc`，彼此不互相依賴、單獨安裝也能用。
    ///
    /// 三件事：
    ///
    /// 1. **詞條表**：每個 mod 用 <see cref="Bind{T}"/> 綁設定時順手登記英文名稱與
    ///    中英說明。key 一律是**原本那串中文**——BepInEx 的 section / key 就是 .cfg
    ///    的欄位名，是識別字不是顯示字，翻譯它們等於換一次語言就把整份設定當成
    ///    新項目重生，使用者調好的值全部掉回預設。
    ///
    /// 2. **給 ConfigMenu 查的固定入口** <see cref="Term"/>。ConfigMenu 不認識任何
    ///    特定 mod，它是用反射找每個插件組件裡的 `HtF.Shared.Loc.Term`——有就用，
    ///    沒有就顯示原字串。所以新 mod 只要編進這個檔案，設定頁面就自動有雙語，
    ///    ConfigMenu 那邊一行都不用改。**這個方法的名稱與簽章請勿更動。**
    ///
    /// 3. **語言判定** <see cref="Resolve"/>，只有自己有 UI 的 mod 需要呼叫
    ///    （ConfigMenu、DazedTools、HudNumbers）。全部 mod 共用 ConfigMenu 那一個
    ///    「語言」設定，沒裝 ConfigMenu 就跟著遊戲語系走。
    /// </summary>
    public static class Loc
    {
        /// <summary>語言設定放在 ConfigMenu，其他 mod 讀它。沒裝就退回遊戲語系。</summary>
        private const string OwnerGuid = "htf.configmenu";
        private const string OwnerSection = "介面";
        private const string OwnerKey = "語言";

        // ------------------------------------------------------------------ 詞條表

        private sealed class Entry
        {
            internal string NameZh, NameEn, DescZh, DescEn;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>
        /// 綁一個設定並登記它的雙語文字。中文名稱就是 key 本身，所以只要給英文的。
        /// `range` 傳 `AcceptableValueRange`／`AcceptableValueList` 就會有滑桿／限制。
        /// </summary>
        public static ConfigEntry<T> Bind<T>(ConfigFile file, string section, string key, T defaultValue,
                                             string nameEn, string descZh, string descEn = null,
                                             AcceptableValueBase range = null)
        {
            Add(key, null, nameEn, descZh, descEn);
            return file.Bind(section, key, defaultValue, new ConfigDescription(CfgDesc(key), range));
        }

        /// <summary>登記 section 的英文名稱。</summary>
        public static void Section(string section, string nameEn)
        {
            Add(section, null, nameEn, null, null);
        }

        /// <summary>
        /// 登記列舉值的顯示文字。存進 .cfg 的仍然是列舉成員名稱（識別字），
        /// 這裡登記的只是畫面上要顯示什麼。
        /// </summary>
        public static void EnumValue(object value, string nameZh, string nameEn)
        {
            if (value == null) return;
            Add("enum." + value, nameZh, nameEn, null, null);
        }

        public static void Add(string key, string nameZh, string nameEn, string descZh, string descEn)
        {
            if (string.IsNullOrEmpty(key)) return;
            Entries[key] = new Entry { NameZh = nameZh, NameEn = nameEn, DescZh = descZh, DescEn = descEn };
        }

        /// <summary>
        /// **ConfigMenu 用反射呼叫的入口，勿改名或改簽章。**
        /// 查不到就回 null，呼叫端會退回顯示原字串。
        /// </summary>
        public static string Term(string key, bool english, bool wantDesc)
        {
            Entry e;
            if (key == null || !Entries.TryGetValue(key, out e)) return null;
            if (wantDesc) return english ? e.DescEn : e.DescZh;
            return english ? e.NameEn : e.NameZh;
        }

        /// <summary>
        /// 寫進 .cfg 的說明。那個檔案沒辦法跟著語言重畫（BepInEx 只在 Bind 當下
        /// 寫一次註解），所以中英併列——換行會被寫成另一行 `##`，讀起來剛好。
        /// </summary>
        public static string CfgDesc(string key)
        {
            Entry e;
            if (key == null || !Entries.TryGetValue(key, out e) || e.DescZh == null) return "";
            return e.DescEn == null ? e.DescZh : e.DescZh + "\n" + e.DescEn;
        }

        // ------------------------------------------------------------------ 目前語言

        /// <summary>ConfigMenu 自己設這個（它就是語言設定的擁有者），回傳列舉成員名稱。</summary>
        public static Func<string> LocalChoice;

        public static bool IsEnglish { get; private set; }

        private static ConfigEntryBase _remote;
        private static string _remoteChoice;
        private static float _nextRemoteRead;

        /// <summary>
        /// 重新判定語言。純比較加一次字典查詢，可以每幀呼叫。
        /// 只有自己要畫 UI 的 mod 需要呼叫——別人的設定名稱是 ConfigMenu 在畫的，
        /// 它會把自己判定的語言當參數傳進 <see cref="Term"/>。
        /// </summary>
        public static void Resolve()
        {
            string choice = LocalChoice != null ? LocalChoice() : RemoteChoice();
            if (choice == "English") IsEnglish = true;
            else if (choice == "Chinese") IsEnglish = false;
            else IsEnglish = !GameSpeaksChinese();
        }

        /// <summary>沒有自己的語言設定時，去 ConfigMenu 借。它沒裝就回 null。</summary>
        private static string RemoteChoice()
        {
            if (Time.unscaledTime < _nextRemoteRead) return _remoteChoice;
            _nextRemoteRead = Time.unscaledTime + 0.5f;

            try
            {
                if (_remote == null)
                {
                    PluginInfo info;
                    if (!Chainloader.PluginInfos.TryGetValue(OwnerGuid, out info)) return _remoteChoice = null;
                    if (info == null || info.Instance == null || info.Instance.Config == null) return _remoteChoice = null;

                    foreach (ConfigEntryBase e in info.Instance.Config.GetConfigEntries())
                    {
                        if (e.Definition.Section == OwnerSection && e.Definition.Key == OwnerKey) { _remote = e; break; }
                    }
                    if (_remote == null) return _remoteChoice = null;
                }

                object v = _remote.BoxedValue;
                _remoteChoice = v != null ? v.ToString() : null;
            }
            catch (Exception) { _remoteChoice = null; }

            return _remoteChoice;
        }

        /// <summary>
        /// 遊戲的語系索引出自 LocalizationManager.SetToSteamLanguage 的對照表：
        /// 2 = 簡體、3 = 繁體，其餘一律當英文。
        /// 它是靜態屬性，語系還沒初始化時回 0（英文），所以不會 NRE。
        /// </summary>
        private static bool GameSpeaksChinese()
        {
            try
            {
                int i = LocalizationManager.CurLanguage;
                return i == 2 || i == 3;
            }
            catch (Exception) { return false; }
        }

        /// <summary>挑語言。自己的 UI 文字都經過這裡。</summary>
        public static string P(string zh, string en) { return IsEnglish ? en : zh; }
    }
}

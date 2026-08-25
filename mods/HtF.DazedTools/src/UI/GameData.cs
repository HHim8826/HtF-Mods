using System;
using System.Collections.Generic;
using HtF.Shared;

namespace HtF.DazedTools.UI
{
    internal readonly struct ItemEntry
    {
        public readonly int Id;
        public readonly string Key;      // /spawn 用的鍵（去空格、全小寫）
        public readonly string Display;  // prefab 原始名稱
        public readonly string Kind;     // 分類（魚 / 武器 / 工具…）

        public ItemEntry(int id, string key, string display, string kind)
        {
            Id = id; Key = key; Display = display; Kind = kind;
        }

        public string Label { get { return Id + "  " + Display; } }

        /// <summary>分類的顯示文字。GameData.Items.cs 是產生出來的，不在那裡放翻譯。</summary>
        public string KindText { get { return GameData.KindLabel(Kind); } }
    }

    /// <summary>
    /// 對照表資料。數值全部出自 DAZED_COMMANDS_手冊.md 第 4 節，
    /// 那些數值是從遊戲資產實際抽取的，不是憑印象填的。
    ///
    /// 表格是**選單標籤**，開頭的數字就是要送出的參數值（見 <see cref="IndexOf"/>），
    /// 所以中英兩份的數字必須一模一樣，只有後面的說明文字換語言。
    /// </summary>
    internal static partial class GameData
    {
        // --- 魚餌（索引 0 會自踢，UI 一律從 1 起跳）---
        private static readonly string[] BaitsZh =
        {
            "0  (沒有餌) — 會自踢",
            "1  (沒有餌，重複項)",
            "2  Fish Bucket",
            "3  Testing",
            "4  Hot Dog",
            "5  Leech Bait",
            "6  Beginner Lure",
            "7  Empty Beer Can",
            "8  Standard Lure",
            "9  Beginner Boss Lure",
            "10  Professional Lure",
            "11  Carrot",
            "12  Standard Boss Lure",
            "13  Coconut",
            "14  Scientific Lure",
            "15  Professional Boss Lure",
            "16  Scientific Boss Lure",
        };
        private static readonly string[] BaitsEn =
        {
            "0  (no bait) — kicks you",
            "1  (no bait, duplicate)",
            "2  Fish Bucket",
            "3  Testing",
            "4  Hot Dog",
            "5  Leech Bait",
            "6  Beginner Lure",
            "7  Empty Beer Can",
            "8  Standard Lure",
            "9  Beginner Boss Lure",
            "10  Professional Lure",
            "11  Carrot",
            "12  Standard Boss Lure",
            "13  Coconut",
            "14  Scientific Lure",
            "15  Professional Boss Lure",
            "16  Scientific Boss Lure",
        };
        internal static string[] Baits { get { return Loc.IsEnglish ? BaitsEn : BaitsZh; } }
        internal const int BaitMin = 1;
        internal const int BaitMax = 16;

        // --- 武器配件（依資源名稱字母排序）---
        private static readonly string[] AttachmentsZh =
        {
            "0  Compensator — 中幅降低後座力",
            "1  Extended Magazine — 增加彈匣容量",
            "2  Iron Sight — 移除已裝的瞄具",
            "3  Laser Sight — 腰射有指示點",
            "4  Red Dot Sight — 瞄準更好命中",
            "5  Sniper Scope — 遠距瞄準更好命中",
            "6  Suppressor — 大幅降低後座力",
        };
        private static readonly string[] AttachmentsEn =
        {
            "0  Compensator — moderate recoil reduction",
            "1  Extended Magazine — larger magazine",
            "2  Iron Sight — removes the fitted optic",
            "3  Laser Sight — dot while hip firing",
            "4  Red Dot Sight — better aimed accuracy",
            "5  Sniper Scope — better accuracy at range",
            "6  Suppressor — large recoil reduction",
        };
        internal static string[] Attachments { get { return Loc.IsEnglish ? AttachmentsEn : AttachmentsZh; } }

        // --- 口袋槽位（索引 0 會自踢）---
        private static readonly string[] PocketsZh =
        {
            "0  — 會自踢",
            "1  Satchel（背包）— 扣 5",
            "2  Second Pocket — 扣 10",
            "3  Third Pocket — 扣 25",
            "4  Fourth Pocket — 扣 50",
            "5  Fifth Pocket — 扣 100",
        };
        private static readonly string[] PocketsEn =
        {
            "0  — kicks you",
            "1  Satchel — costs 5",
            "2  Second Pocket — costs 10",
            "3  Third Pocket — costs 25",
            "4  Fourth Pocket — costs 50",
            "5  Fifth Pocket — costs 100",
        };
        internal static string[] Pockets { get { return Loc.IsEnglish ? PocketsEn : PocketsZh; } }
        internal const int PocketMin = 1;
        internal const int PocketMax = 5;

        // --- 船馬達（只能升級，不能降級）---
        private static readonly string[] MotorsZh =
        {
            "0  小型 — 推力 800",
            "1  中型 — 推力 1400",
            "2  大型 — 推力 2000",
            "3  大型強化 — 推力 3000",
        };
        private static readonly string[] MotorsEn =
        {
            "0  Small — 800 thrust",
            "1  Medium — 1400 thrust",
            "2  Large — 2000 thrust",
            "3  Large Upgraded — 3000 thrust",
        };
        internal static string[] Motors { get { return Loc.IsEnglish ? MotorsEn : MotorsZh; } }

        // --- NPC（255 會自踢）---
        internal static readonly string[] Npcs =
        {
            "0  Default NPC",
            "1  Lighthouse NPC",
            "2  Swamp Daughter NPC",
            "3  Swamp NPC",
            "4  Grillmaster NPC",
            "5  Kiosk NPC",
            "6  Pufferfish NPC",
            "7  Roulette NPC",
            "8  Seagull NPC",
            "9  Slots NPC",
            "10  StoreClerc NPC",
            "11  WeaponSeller NPC",
            "12  Military NPC",
            "13  Military NPC 2",
            "14  Whale NPC",
        };

        // --- 賭盤顏色 ---
        private static readonly string[] RouletteColorsZh = { "0  黑 Black", "1  紅 Red", "2  綠 Green" };
        private static readonly string[] RouletteColorsEn = { "0  Black", "1  Red", "2  Green" };
        internal static string[] RouletteColors { get { return Loc.IsEnglish ? RouletteColorsEn : RouletteColorsZh; } }

        // --- 島嶼 ---
        private static readonly string[] IslandsZh =
        {
            "0  Island1", "1  Island2", "2  Island3", "3  Island4",
            "4  Island5（最終島）", "5  DevIsland（不在循環內）",
        };
        private static readonly string[] IslandsEn =
        {
            "0  Island1", "1  Island2", "2  Island3", "3  Island4",
            "4  Island5 (final island)", "5  DevIsland (outside the rotation)",
        };
        internal static string[] Islands { get { return Loc.IsEnglish ? IslandsEn : IslandsZh; } }

        /// <summary>
        /// 物品分類的英文對照。`GameData.Items.cs` 是由手冊自動產生的（不要手改），
        /// 所以翻譯放在這裡查表，不動那份產生出來的資料。
        /// </summary>
        private static readonly Dictionary<string, string> KindEn = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "魚", "Fish" },
            { "攻擊性魚", "Aggressive fish" },
            { "逃跑魚", "Fleeing fish" },
            { "食人魚", "Piranha" },
            { "河豚", "Pufferfish" },
            { "生物", "Creature" },
            { "螃蟹", "Crab" },
            { "蜘蛛蟹", "Spider crab" },
            { "弓頭鯨", "Bowhead whale" },
            { "信天翁", "Albatross" },
            { "鳥", "Bird" },
            { "屍體", "Corpse" },
            { "道具", "Tool" },
            { "槍械", "Firearm" },
            { "近戰", "Melee" },
            { "爆裂物", "Explosive" },
            { "魚竿", "Fishing rod" },
            { "蟹竿", "Crab rod" },
            { "收音機", "Radio" },
            { "地圖", "Map" },
            { "飛盤", "Frisbee" },
        };

        internal static string KindLabel(string kind)
        {
            if (!Loc.IsEnglish || string.IsNullOrEmpty(kind)) return kind;
            string en;
            return KindEn.TryGetValue(kind, out en) ? en : kind;
        }

        /// <summary>下拉選單標籤前面的數字就是要送出的參數值。</summary>
        internal static string IndexOf(string label)
        {
            int i = label.IndexOf(' ');
            return i < 0 ? label : label.Substring(0, i);
        }
    }
}

using HtF.Shared;

namespace HtF.HudNumbers
{
    /// <summary>
    /// HUD 上的文字。底層是共用的 <see cref="Loc"/>（`mods/Shared/Loc.cs`）：
    /// 語言跟著 ConfigMenu 的「語言」設定走，沒裝 ConfigMenu 就跟著遊戲語系。
    ///
    /// 設定項的英文名稱與說明不在這裡——它們寫在 `Plugin.Awake` 的
    /// <see cref="Loc.Bind{T}"/> 呼叫裡，一個設定一行。
    /// </summary>
    internal static class L
    {
        internal static string Money { get { return Loc.P("金錢", "Money"); } }
        internal static string Health { get { return Loc.P("生命", "Health"); } }
        internal static string Fullness { get { return Loc.P("飽食", "Fullness"); } }
        internal static string Poison { get { return Loc.P("中毒", "Poison"); } }
        internal static string Fire { get { return Loc.P("著火", "Burning"); } }

        internal static string HeldHead { get { return Loc.P("— 手上 —", "— Held —"); } }
        internal static string AimHead { get { return Loc.P("— 準心 —", "— Crosshair —"); } }

        internal static string Status { get { return Loc.P("狀態", "Status"); } }
        internal static string Dead { get { return Loc.P("已死亡", "Dead"); } }
        internal static string Hp { get { return Loc.P("血量", "HP"); } }
        internal static string Worth { get { return Loc.P("售價", "Value"); } }
        internal static string BaseWorth { get { return Loc.P("基礎", "base"); } }
        internal static string Weight { get { return Loc.P("重量", "Weight"); } }
        internal static string Cookness { get { return Loc.P("熟度", "Cooked"); } }
        internal static string ScoreMul { get { return Loc.P("分數倍率", "Score mult."); } }

        internal static string Endangered { get { return Loc.P("[瀕危]", "[ENDANGERED]"); } }
        internal static string Unnamed { get { return Loc.P("(無名)", "(unnamed)"); } }
        internal static string ReadFailed { get { return Loc.P("讀取失敗：", "Read failed: "); } }
    }
}

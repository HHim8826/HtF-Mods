using HtF.Shared;

namespace HtF.AmmoCounter
{
    /// <summary>
    /// 畫面上的文字。底層是共用的 <see cref="Loc"/>（`mods/Shared/Loc.cs`）：
    /// 語言跟著 ConfigMenu 的「語言」設定走，沒裝 ConfigMenu 就跟著遊戲語系。
    ///
    /// 設定項的英文名稱與說明不在這裡——它們寫在 `Plugin.Awake` 的
    /// <see cref="Loc.Bind{T}"/> 呼叫裡，一個設定一行。
    /// </summary>
    internal static class L
    {
        internal static string Reloading { get { return Loc.P("裝填中…", "Reloading…"); } }
        internal static string Unnamed { get { return Loc.P("(無名)", "(unnamed)"); } }
    }
}

namespace Island.Core;

/// <summary>
/// 外殼提供的主題筆刷資源鍵。模組在 XAML 用 <c>{DynamicResource Island.Accent}</c>,
/// 程式碼用 <c>element.SetResourceReference(prop, IslandTheme.Accent)</c>。
/// 顏色會隨「強調色」改變(例如跟著正在播放的專輯封面),所以一定要用 DynamicResource。
/// 沒有強調色時是中性的黑白灰。
/// </summary>
public static class IslandTheme
{
    /// <summary>強調色本體:進度條、音量條、開啟的開關。</summary>
    public const string Accent = "Island.Accent";
    /// <summary>淡強調色:選中分頁的文字、控制按鈕圖示。</summary>
    public const string AccentSoft = "Island.AccentSoft";
    /// <summary>暗強調色:選中分頁的底色、按鈕滑過的底色。</summary>
    public const string AccentDim = "Island.AccentDim";
    /// <summary>滑桿 / 進度條未填滿的軌道。</summary>
    public const string AccentTrack = "Island.AccentTrack";
    /// <summary>帶一點強調色的次要文字(時間、數字)。</summary>
    public const string AccentMuted = "Island.AccentMuted";
    /// <summary>展開面板的底色。</summary>
    public const string PanelBackground = "Island.PanelBackground";
    /// <summary>展開面板的邊框。</summary>
    public const string PanelBorder = "Island.PanelBorder";
    /// <summary>收合 / 預覽膠囊的邊框。</summary>
    public const string CapsuleBorder = "Island.CapsuleBorder";
    /// <summary>放在 <see cref="Accent"/> 上面的前景色(中性時強調色是白色,前景就是黑色)。</summary>
    public const string OnAccent = "Island.OnAccent";
}

using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組在「設定」視窗裡有自己的一頁(例如天氣的城市、Shelf 的截圖選項)。
/// 畫面可以用 <see cref="IslandTheme"/> 的設定樣式鍵(SettingsToggle 等)維持一致的外觀。
/// </summary>
public interface IIslandSettingsPage
{
    /// <summary>左側選單上的名稱。</summary>
    string SettingsTitle { get; }

    /// <summary>設定頁內容。每次開啟設定視窗都會呼叫一次。</summary>
    FrameworkElement CreateSettingsView();
}

using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組有「正在進行的活動」(例如正在播放音樂)時,
/// 外殼會在閒置狀態也顯示膠囊,並用 <see cref="CreateActivityView"/> 取代平常的收合內容。
/// 點擊膠囊時會直接展開到這個模組。
/// </summary>
public interface IIslandActivity
{
    /// <summary>目前是否有活動。只能在 UI 執行緒讀取。</summary>
    bool IsActivityActive { get; }

    /// <summary><see cref="IsActivityActive"/> 改變時觸發(在 UI 執行緒上)。</summary>
    event EventHandler? ActivityChanged;

    /// <summary>活動時顯示在收合膠囊裡的畫面(高度約 34 DIP)。外殼只呼叫一次。</summary>
    FrameworkElement? CreateActivityView();
}

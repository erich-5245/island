using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組有「正在進行的活動」(例如正在播放音樂)時,
/// 滑鼠移過去(預覽)會改顯示 <see cref="CreateActivityView"/> 取代平常的一排字,
/// 點擊膠囊時直接展開到這個模組。
/// 想在閒置時短暫出現(例如換歌),呼叫 <see cref="IIslandHost.Announce"/>。
/// </summary>
public interface IIslandActivity
{
    /// <summary>目前是否有活動。只能在 UI 執行緒讀取。</summary>
    bool IsActivityActive { get; }

    /// <summary><see cref="IsActivityActive"/> 改變時觸發(在 UI 執行緒上)。</summary>
    event EventHandler? ActivityChanged;

    /// <summary>活動時顯示在膠囊裡的畫面(一排,高度約 34 DIP,寬度為預覽寬度)。外殼只呼叫一次。</summary>
    FrameworkElement? CreateActivityView();
}

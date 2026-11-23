using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組可以縮成一個「小圓」顯示在長條兩側(像 iPhone 動態島同時顯示多個即時動態)。
/// 滑鼠移到島上時,外殼依設定的優先順序(liveOrder)選出中間長條要顯示的活動,
/// 其餘「目前有東西可看」的模組最多兩個,放在左右小圓。
/// 相關度改變時請觸發 <see cref="IIslandActivity.ActivityChanged"/> 或 <see cref="IIslandModule.BadgeChanged"/>,
/// 外殼會重新排列。
/// </summary>
public interface IIslandCompact
{
    /// <summary>現在是否值得佔一個小圓(例如計時中、正在播放、Shelf 有檔案)。只在 UI 執行緒讀取。</summary>
    bool IsCompactRelevant { get; }

    /// <summary>小圓內的畫面(約 34×34 DIP,外殼會裁成圓形)。外殼只呼叫一次。</summary>
    FrameworkElement? CreateCompactView();

    /// <summary>
    /// 沒有任何活動(中間長條是日期 / 時間那一排)時也顯示這個小圓,例如「捷徑」入口。
    /// 預設 false:一般模組只在中間有活動時才放進小圓。
    /// </summary>
    bool ShowWithoutActivity => false;
}

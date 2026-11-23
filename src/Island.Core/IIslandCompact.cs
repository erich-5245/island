using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組可以縮成「小圓」顯示在長條兩側(像 iPhone 動態島同時顯示多個即時動態)。
/// 滑鼠移到島上時,外殼依設定的優先順序(liveOrder)選出中間長條要顯示的活動,
/// 其餘「目前有東西可看」的模組最多兩個小圓,放在左右。
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

    // ── 一個模組可以有多個小圓(例如兩個捷徑) ──

    /// <summary>最多幾個小圓。</summary>
    int CompactCount => 1;

    /// <summary>第 index 個小圓現在是否要顯示。</summary>
    bool IsCompactRelevantAt(int index) => index == 0 && IsCompactRelevant;

    /// <summary>第 index 個小圓的畫面(外殼對每個 index 只呼叫一次)。</summary>
    FrameworkElement? CreateCompactView(int index) => index == 0 ? CreateCompactView() : null;

    // ── 點小圓 ──

    /// <summary>
    /// 點小圓時「直接執行」的動作名稱(例如「播放 / 暫停」),顯示在設定裡讓使用者選。
    /// null = 沒有直接動作,點小圓只會打開分頁。
    /// </summary>
    string? CompactActionLabel => null;

    /// <summary>使用者沒有設定時,點小圓預設是否直接執行(捷徑是 true,其他是 false = 打開分頁)。</summary>
    bool CompactActionByDefault => false;

    /// <summary>執行第 index 個小圓的直接動作。</summary>
    void RunCompactAction(int index) { }
}

/// <summary>
/// 選用介面:接收 Ctrl+Alt+1…9 全域快捷鍵(例如捷徑模組:第 n 個捷徑)。
/// 外殼在設定開啟時註冊,只有一個模組會收到。
/// </summary>
public interface IIslandNumberHotkeys
{
    /// <summary>number = 1…9。</summary>
    void OnNumberHotkey(int number);
}

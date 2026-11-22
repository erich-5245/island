using System.Windows.Threading;

namespace Island.Core;

/// <summary>外殼提供給模組的服務。之後需要的能力加在這裡。</summary>
public interface IIslandHost
{
    /// <summary>UI 執行緒的 Dispatcher。</summary>
    Dispatcher Dispatcher { get; }

    /// <summary>此模組專屬的資料夾:%LOCALAPPDATA%\Island\modules\&lt;id&gt;(已建立)。</summary>
    string GetModuleDataDirectory(string moduleId);

    /// <summary>寫一行到 %LOCALAPPDATA%\Island\island.log。</summary>
    void Log(string message);

    /// <summary>展開島並切換到指定模組。</summary>
    void Expand(string moduleId);

    /// <summary>收合島(有 <see cref="HoldExpanded"/> 未釋放時會被忽略)。</summary>
    void Collapse();

    /// <summary>
    /// 在回傳的物件 Dispose 之前,島不會自動收合。
    /// 例如從 Shelf 拖檔案出去的期間:<c>using (host.HoldExpanded()) DragDrop.DoDragDrop(...)</c>。
    /// </summary>
    IDisposable HoldExpanded();

    /// <summary>
    /// 短暫顯示這個模組的活動畫面(<see cref="IIslandActivity.CreateActivityView"/>),
    /// 例如換歌時讓膠囊出現 3 秒再隱藏。島已經展開或滑鼠正在島上時不會有動作。
    /// </summary>
    void Announce(string moduleId, TimeSpan duration);

    /// <summary>
    /// 設定整個島的強調色(見 <see cref="IslandTheme"/>),例如正在播放的專輯主色;
    /// null = 回到中性黑白。可從任何執行緒呼叫。
    /// </summary>
    void SetAccent(System.Windows.Media.Color? color);

    /// <summary>
    /// 把資料(例如檔案)交給另一個模組,就像使用者把它拖到那個模組上一樣
    /// (對方要實作 <see cref="IIslandDropTarget"/>)。例如下載完成後放進 Shelf。成功回傳 true。
    /// </summary>
    bool SendToModule(string moduleId, System.Windows.IDataObject data);
}

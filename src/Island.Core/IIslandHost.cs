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
}

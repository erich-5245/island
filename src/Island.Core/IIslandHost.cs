using System.Windows.Threading;

namespace Island.Core;

/// <summary>外殼提供給模組的服務。之後需要的能力(展開請求、設定讀寫…)加在這裡。</summary>
public interface IIslandHost
{
    /// <summary>UI 執行緒的 Dispatcher。</summary>
    Dispatcher Dispatcher { get; }

    /// <summary>此模組專屬的資料夾:%LOCALAPPDATA%\Island\modules\&lt;id&gt;(已建立)。</summary>
    string GetModuleDataDirectory(string moduleId);

    /// <summary>寫一行到 %LOCALAPPDATA%\Island\island.log。</summary>
    void Log(string message);
}

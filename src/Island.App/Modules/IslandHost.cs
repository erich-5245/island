using System.Windows.Threading;
using Island.App.Infrastructure;
using Island.Core;

namespace Island.App.Modules;

/// <summary>外殼(MainWindow)實作的控制介面,讓 IslandHost 轉交展開/收合要求。</summary>
internal interface IShellController
{
    void Expand(string moduleId);
    void Collapse();
    IDisposable HoldExpanded();
}

internal sealed class IslandHost(Dispatcher dispatcher) : IIslandHost
{
    /// <summary>視窗建立後由 App 設定。</summary>
    public IShellController? Shell { get; set; }

    public Dispatcher Dispatcher { get; } = dispatcher;

    public string GetModuleDataDirectory(string moduleId) => AppPaths.ModuleDataDirectory(moduleId);

    public void Log(string message) => Infrastructure.Log.Write(message);

    public void Expand(string moduleId) => Shell?.Expand(moduleId);

    public void Collapse() => Shell?.Collapse();

    public IDisposable HoldExpanded() => Shell?.HoldExpanded() ?? EmptyDisposable.Instance;

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}

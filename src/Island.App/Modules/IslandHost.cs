using System.Windows.Threading;
using Island.App.Infrastructure;
using Island.Core;

namespace Island.App.Modules;

internal sealed class IslandHost(Dispatcher dispatcher) : IIslandHost
{
    public Dispatcher Dispatcher { get; } = dispatcher;

    public string GetModuleDataDirectory(string moduleId) => AppPaths.ModuleDataDirectory(moduleId);

    public void Log(string message) => Infrastructure.Log.Write(message);
}

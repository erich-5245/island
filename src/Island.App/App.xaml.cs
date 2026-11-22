using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Island.App.Infrastructure;
using Island.App.Modules;
using Island.Core;

namespace Island.App;

public partial class App : Application
{
    private const string MutexName = @"Local\Island.SingleInstance";

    private Mutex? _singleInstance;
    private IReadOnlyList<IIslandModule> _modules = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        // 單一執行個體:已經有一個在跑就直接結束
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write("未處理例外:" + args.Exception);
            args.Handled = true;
        };

        var settings = IslandSettings.Load();
        Log.Write($"Island 啟動 v{typeof(App).Assembly.GetName().Version} pid={Environment.ProcessId}");

        if (settings.SoftwareRendering)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        _modules = ModuleLoader.LoadAll(settings, new IslandHost(Dispatcher));

        var window = new MainWindow(settings, _modules);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (var m in _modules)
        {
            try { m.Dispose(); } catch { /* 結束時忽略 */ }
        }
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}

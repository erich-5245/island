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
        AutoStart.RefreshPathIfEnabled();
        Log.Write($"Island 啟動 v{typeof(App).Assembly.GetName().Version} pid={Environment.ProcessId}");

        if (settings.SoftwareRendering)
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        AccentTheme.Install(Resources);

        var host = new IslandHost(Dispatcher);
        _modules = ModuleLoader.LoadAll(settings, host);

        var window = new MainWindow(settings, _modules);
        host.Shell = window;
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// 重新啟動(設定改了尺寸、模組等需要重建視窗的項目時)。
    /// 先放掉單一執行個體的 Mutex,新的行程才不會以為已經有一個在跑而直接結束。
    /// </summary>
    public void Restart()
    {
        Log.Write("重新啟動");
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
            _singleInstance = null;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Write($"重新啟動失敗:{ex.Message}");
        }
        Shutdown();
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

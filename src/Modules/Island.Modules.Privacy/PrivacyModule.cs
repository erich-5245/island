using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;
using Microsoft.Win32;

namespace Island.Modules.Privacy;

/// <summary>
/// 麥克風 / 鏡頭使用中指示(像 iPhone 的橘點、綠點)。
/// Windows 把「哪個程式正在用麥克風 / 鏡頭」記在
/// HKCU\...\CapabilityAccessManager\ConsentStore\{microphone|webcam}(LastUsedTimeStop = 0 代表使用中),
/// 工作列的麥克風圖示也是讀這裡。用 RegNotifyChangeKeyValue 等登錄變更(事件驅動,不輪詢)。
/// - 有程式開始使用:膠囊出現約 3 秒「● 麥克風使用中 · Teams」;
/// - 使用期間:滑鼠移過去時是一個活動(長條)或小圓(橘點 / 綠點)。
/// </summary>
public sealed class PrivacyModule : IslandModuleBase, IIslandActivity, IIslandCompact
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private static readonly TimeSpan AnnounceFor = TimeSpan.FromSeconds(3);
    public static readonly Color MicColor = Color.FromRgb(0xFF, 0x9F, 0x0A);
    public static readonly Color CamColor = Color.FromRgb(0x30, 0xD1, 0x58);

    private readonly List<RegistryWatcher> _watchers = [];
    private DispatcherTimer? _debounce;
    private PrivacyRow? _row;
    private PrivacyDots? _dots;

    public override string Id => "privacy";
    public override string Title => "隱私";

    public IReadOnlyList<string> MicApps { get; private set; } = [];
    public IReadOnlyList<string> CamApps { get; private set; } = [];

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => MicApps.Count > 0 || CamApps.Count > 0;
    public bool IsCompactRelevant => IsActivityActive;

    protected override void OnInitialize()
    {
        _debounce = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Rescan(announce: true); };
        foreach (var cap in new[] { "microphone", "webcam" })
        {
            var w = new RegistryWatcher($@"{Root}\{cap}", () => Host.Dispatcher.BeginInvoke(() => { _debounce!.Stop(); _debounce.Start(); }));
            if (w.Start()) _watchers.Add(w);
        }
        Rescan(announce: false);
        Host.Log($"隱私:監看 {_watchers.Count} 個登錄位置;目前麥克風 {MicApps.Count}、鏡頭 {CamApps.Count}");
    }

    public FrameworkElement CreateActivityView() => _row ??= new PrivacyRow(this);
    public FrameworkElement CreateCompactView() => _dots ??= new PrivacyDots(this);

    private void Rescan(bool announce)
    {
        var mic = InUse("microphone");
        var cam = InUse("webcam");
        bool newMic = mic.Except(MicApps).Any(), newCam = cam.Except(CamApps).Any();
        bool wasActive = IsActivityActive;
        MicApps = mic;
        CamApps = cam;
        _row?.Update();
        _dots?.Update();
        if (wasActive != IsActivityActive) ActivityChanged?.Invoke(this, EventArgs.Empty);
        if (announce && (newMic || newCam))
        {
            Host.Log($"隱私:麥克風 [{string.Join(", ", mic)}] 鏡頭 [{string.Join(", ", cam)}]");
            Host.Announce(Id, AnnounceFor);
        }
    }

    /// <summary>某個能力(microphone / webcam)目前正在使用的程式名稱。</summary>
    private static List<string> InUse(string capability)
    {
        var apps = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{Root}\{capability}");
            if (key is null) return apps;
            Collect(key, packaged: true, apps);
            using var np = key.OpenSubKey("NonPackaged");
            if (np is not null) Collect(np, packaged: false, apps);
        }
        catch { }
        return apps.Distinct().ToList();
    }

    private static void Collect(RegistryKey parent, bool packaged, List<string> apps)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            if (name == "NonPackaged") continue;
            try
            {
                using var sub = parent.OpenSubKey(name);
                if (sub?.GetValue("LastUsedTimeStart") is not long start || start == 0) continue;
                if (sub.GetValue("LastUsedTimeStop") is long stop && stop != 0) continue;
                apps.Add(packaged ? PackagedName(name) : Path.GetFileNameWithoutExtension(name.Replace('#', '\\')));
            }
            catch { }
        }
    }

    /// <summary>「Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe」→「WindowsSoundRecorder」。</summary>
    private static string PackagedName(string family)
    {
        var name = family.Split('_')[0];
        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }

    public override void Dispose()
    {
        _debounce?.Stop();
        foreach (var w in _watchers) w.Dispose();
        base.Dispose();
    }
}

/// <summary>在背景執行緒等登錄機碼(含子機碼)變更,變更時呼叫 callback。</summary>
internal sealed class RegistryWatcher : IDisposable
{
    private readonly string _path;
    private readonly Action _changed;
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _thread;

    public RegistryWatcher(string path, Action changed)
    {
        _path = path;
        _changed = changed;
    }

    public bool Start()
    {
        RegistryKey? probe = Registry.CurrentUser.OpenSubKey(_path);
        if (probe is null) return false;
        probe.Dispose();
        _thread = new Thread(Run) { IsBackground = true, Name = "Island.RegistryWatcher" };
        _thread.Start();
        return true;
    }

    private void Run()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_path, writable: false);
            if (key is null) return;
            using var changed = new AutoResetEvent(false);
            var handle = key.Handle.DangerousGetHandle();
            while (true)
            {
                int rc = RegNotifyChangeKeyValue(handle, true, REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET,
                    changed.SafeWaitHandle.DangerousGetHandle(), true);
                if (rc != 0) return;
                if (WaitHandle.WaitAny([changed, _stop]) == 1) return;
                try { _changed(); } catch { }
            }
        }
        catch { /* 監看失敗就算了,不影響其他功能 */ }
    }

    public void Dispose()
    {
        _stop.Set();
    }

    private const uint REG_NOTIFY_CHANGE_NAME = 0x1, REG_NOTIFY_CHANGE_LAST_SET = 0x4;

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(IntPtr key, bool watchSubtree, uint filter, IntPtr evt, bool asynchronous);
}

/// <summary>活動的一排:● 麥克風使用中 · Teams   ● 鏡頭</summary>
internal sealed class PrivacyRow : Grid
{
    private readonly PrivacyModule _module;
    private readonly StackPanel _left = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _apps = new()
    {
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 2, 0),
    };

    public PrivacyRow(PrivacyModule module)
    {
        _module = module;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_left);
        SetColumn(_apps, 1);
        Children.Add(_apps);
        Update();
    }

    public void Update()
    {
        _left.Children.Clear();
        bool mic = _module.MicApps.Count > 0, cam = _module.CamApps.Count > 0;
        if (mic) AddItem(PrivacyModule.MicColor, cam ? "麥克風" : "麥克風使用中");
        if (cam) AddItem(PrivacyModule.CamColor, mic ? "鏡頭" : "鏡頭使用中");
        _apps.Text = string.Join("、", _module.MicApps.Concat(_module.CamApps).Distinct());
    }

    private void AddItem(Color color, string text)
    {
        _left.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 9, Height = 9, Fill = new SolidColorBrush(color),
            Margin = new Thickness(_left.Children.Count == 0 ? 4 : 12, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center,
        });
        _left.Children.Add(new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
    }
}

/// <summary>小圓:橘點(麥克風)/ 綠點(鏡頭),兩個都有就並排。</summary>
internal sealed class PrivacyDots : StackPanel
{
    private readonly PrivacyModule _module;

    public PrivacyDots(PrivacyModule module)
    {
        _module = module;
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Update();
    }

    public void Update()
    {
        Children.Clear();
        if (_module.MicApps.Count > 0) Add(PrivacyModule.MicColor);
        if (_module.CamApps.Count > 0) Add(PrivacyModule.CamColor);
    }

    private void Add(Color c) => Children.Add(new System.Windows.Shapes.Ellipse
    {
        Width = 9, Height = 9, Fill = new SolidColorBrush(c), Margin = new Thickness(2, 0, 2, 0),
    });
}

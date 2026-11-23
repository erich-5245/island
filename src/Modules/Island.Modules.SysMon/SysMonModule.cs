using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.SysMon;

/// <summary>
/// 系統監看:CPU、記憶體、網路速度。預設關閉(設定 →「模組」打開)。
/// 打開後滑鼠移到島上時會有一顆 CPU 小圓(沒有其他活動時也顯示),展開「系統」分頁看三項詳細數字。
/// 效能:只在小圓或分頁「看得到」時每秒取樣一次;看不到時計時器停止,完全不耗資源。
/// </summary>
public sealed class SysMonModule : IslandModuleBase, IIslandCompact
{
    private DispatcherTimer? _timer;
    private readonly SysSampler _sampler = new();
    private SysMonView? _view;
    private SysMonCircle? _circle;
    private int _visibleViews;

    public override string Id => "sysmon";
    public override string Title => "系統";
    public override bool EnabledByDefault => false;

    public SysSample Last { get; private set; }

    protected override void OnInitialize()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Sample();
    }

    public bool IsCompactRelevant => true;
    public bool ShowWithoutActivity => true;
    public FrameworkElement CreateCompactView() => _circle ??= Track(new SysMonCircle());
    public override FrameworkElement CreateExpandedView() => _view ??= Track(new SysMonView());

    public string? CompactActionLabel => "打開工作管理員";

    public void RunCompactAction(int index)
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
        catch (Exception ex) { Host.Log($"系統:打開工作管理員失敗:{ex.Message}"); }
    }

    /// <summary>畫面出現 / 消失時開關取樣。</summary>
    private T Track<T>(T view) where T : FrameworkElement
    {
        view.IsVisibleChanged += (_, e) =>
        {
            _visibleViews += (bool)e.NewValue ? 1 : -1;
            if (_visibleViews < 0) _visibleViews = 0;
            if (_visibleViews > 0 && !_timer!.IsEnabled)
            {
                _sampler.Reset();   // 隔了一段時間才看:先重設基準,避免第一筆是很久的平均
                Sample();
                _timer.Start();
            }
            else if (_visibleViews == 0) _timer!.Stop();
        };
        return view;
    }

    private void Sample()
    {
        Last = _sampler.Take();
        _circle?.Show(Last);
        _view?.Show(Last);
    }

    public override void Dispose()
    {
        _timer?.Stop();
        base.Dispose();
    }
}

/// <summary>一次取樣的結果。CpuPercent / MemoryPercent 是 0–100;網路是每秒位元組。</summary>
public readonly record struct SysSample(double CpuPercent, double MemoryPercent, double DownBytesPerSec, double UpBytesPerSec, bool Valid);

/// <summary>讀系統數字(GetSystemTimes、GlobalMemoryStatusEx、網路介面統計),算出兩次之間的差。</summary>
internal sealed class SysSampler
{
    private ulong _idle, _kernel, _user;
    private long _rx, _tx;
    private long _at;
    private bool _has;

    public void Reset() => _has = false;

    public SysSample Take()
    {
        GetSystemTimes(out var idleFt, out var kernelFt, out var userFt);
        ulong idle = ToUlong(idleFt), kernel = ToUlong(kernelFt), user = ToUlong(userFt);
        var (rx, tx) = NetTotals();
        long now = Stopwatch.GetTimestamp();

        double cpu = 0, down = 0, up = 0;
        if (_has)
        {
            cpu = SysMath.CpuPercent(_idle, _kernel, _user, idle, kernel, user);
            double secs = (now - _at) / (double)Stopwatch.Frequency;
            down = SysMath.Rate(_rx, rx, secs);
            up = SysMath.Rate(_tx, tx, secs);
        }
        bool valid = _has;
        (_idle, _kernel, _user, _rx, _tx, _at, _has) = (idle, kernel, user, rx, tx, now, true);
        return new SysSample(cpu, MemoryLoad(), down, up, valid);
    }

    private static (long Rx, long Tx) NetTotals()
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var st = ni.GetIPStatistics();
                rx += st.BytesReceived;
                tx += st.BytesSent;
            }
        }
        catch { /* 讀不到就當 0 */ }
        return (rx, tx);
    }

    private static double MemoryLoad()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m) ? m.dwMemoryLoad : 0;
    }

    private static ulong ToUlong(FILETIME f) => ((ulong)f.High << 32) | f.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low, High; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}

/// <summary>純計算(方便測試)。</summary>
public static class SysMath
{
    /// <summary>kernel 時間包含 idle;CPU% = 1 − Δidle / (Δkernel + Δuser)。</summary>
    public static double CpuPercent(ulong idle0, ulong kernel0, ulong user0, ulong idle1, ulong kernel1, ulong user1)
    {
        double total = (double)(kernel1 - kernel0) + (user1 - user0);
        if (total <= 0 || idle1 < idle0 || kernel1 < kernel0 || user1 < user0) return 0;
        return Math.Clamp((1 - (idle1 - idle0) / total) * 100, 0, 100);
    }

    /// <summary>每秒位元組;計數器歸零(網卡重新連線)時回傳 0。</summary>
    public static double Rate(long before, long after, double seconds) =>
        seconds <= 0 || after < before ? 0 : (after - before) / seconds;

    /// <summary>"0 B/s"、"512 KB/s"、"1.2 MB/s"、"12 MB/s"。</summary>
    public static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec:0} B/s";
        double kb = bytesPerSec / 1024;
        if (kb < 1024) return $"{kb:0} KB/s";
        double mb = kb / 1024;
        return mb < 10 ? $"{mb:0.0} MB/s" : $"{mb:0} MB/s";
    }
}

/// <summary>小圓:CPU 使用率的進度環 + 數字。</summary>
internal sealed class SysMonCircle : Grid
{
    private readonly Path _arc = new() { StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly TextBlock _text = new() { FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private const double Size = 26;

    public SysMonCircle()
    {
        Width = Height = 30;
        ToolTip = "CPU 使用率";
        var track = new Ellipse { Width = Size, Height = Size, StrokeThickness = 2.5 };
        track.SetResourceReference(Shape.StrokeProperty, IslandTheme.AccentTrack);
        _arc.SetResourceReference(Shape.StrokeProperty, IslandTheme.Accent);
        Children.Add(track);
        Children.Add(new Canvas { Width = Size, Height = Size, Children = { _arc } });
        Children.Add(_text);
        Show(default);
    }

    public void Show(SysSample s)
    {
        _text.Text = s.Valid ? $"{s.CpuPercent:0}" : "…";
        _arc.Data = Arc(s.Valid ? s.CpuPercent / 100 : 0);
    }

    private static Geometry Arc(double fraction)
    {
        fraction = Math.Clamp(fraction, 0.001, 0.999);
        double r = (Size - 2.5) / 2, c = Size / 2;
        double angle = fraction * 2 * Math.PI;
        var start = new Point(c, c - r);
        var end = new Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle));
        var fig = new PathFigure { StartPoint = start, IsClosed = false };
        fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        var g = new PathGeometry([fig]);
        g.Freeze();
        return g;
    }
}

/// <summary>展開的「系統」分頁:CPU、記憶體兩條長條 + 網路上下行速度。</summary>
internal sealed class SysMonView : StackPanel
{
    private readonly (TextBlock Value, Border Fill) _cpu, _mem;
    private readonly TextBlock _net;

    public SysMonView()
    {
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(8, 0, 8, 6);
        _cpu = Row("CPU");
        _mem = Row("記憶體");
        _net = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), Margin = new Thickness(0, 10, 0, 0) };
        Children.Add(_net);
        Show(default);
    }

    private (TextBlock, Border) Row(string label)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grid.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
        track.SetResourceReference(Border.BackgroundProperty, IslandTheme.AccentTrack);
        var fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        fill.SetResourceReference(Border.BackgroundProperty, IslandTheme.Accent);
        var bar = new Grid { Children = { track, fill } };
        Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);
        var value = new TextBlock { FontSize = 12, Foreground = Brushes.White, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        Children.Add(grid);
        bar.SizeChanged += (_, _) => fill.Width = bar.ActualWidth * ((double?)fill.Tag ?? 0);
        return (value, fill);
    }

    public void Show(SysSample s)
    {
        Set(_cpu, s.Valid ? s.CpuPercent : (double?)null);
        Set(_mem, s.MemoryPercent);
        _net.Text = s.Valid ? $"網路  ↓ {SysMath.FormatRate(s.DownBytesPerSec)}   ↑ {SysMath.FormatRate(s.UpBytesPerSec)}" : "網路  …";
    }

    private static void Set((TextBlock Value, Border Fill) row, double? percent)
    {
        row.Value.Text = percent is { } p ? $"{p:0}%" : "…";
        double f = Math.Clamp((percent ?? 0) / 100, 0, 1);
        row.Fill.Tag = f;
        if (row.Fill.Parent is FrameworkElement bar) row.Fill.Width = bar.ActualWidth * f;
    }
}

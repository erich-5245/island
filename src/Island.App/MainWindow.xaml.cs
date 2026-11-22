using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Island.App.Infrastructure;
using Island.App.Interop;
using Island.Core;
using static Island.App.Interop.NativeMethods;

namespace Island.App;

public partial class MainWindow : Window
{
    private readonly IslandSettings _settings;
    private readonly IReadOnlyList<IIslandModule> _modules;
    private IntPtr _hwnd;
    private TopmostKeeper? _topmost;
    private DisplayWatcher? _displays;

    internal MainWindow(IslandSettings settings, IReadOnlyList<IIslandModule> modules)
    {
        _settings = settings;
        _modules = modules;
        InitializeComponent();
        BuildCollapsedContent();
        Closed += (_, _) => { _topmost?.Dispose(); _displays?.Dispose(); };
    }

    // ── 視窗建立 ──────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // 在第一次顯示「之前」套用:不出現在 Alt+Tab / 工作列、點擊不啟用(不搶焦點)
        var ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        ex = (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex));

        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);

        _topmost = new TopmostKeeper(_hwnd);
        _displays = new DisplayWatcher(Dispatcher, () => Reposition("螢幕變更"));
        Reposition("啟動");
        Loaded += (_, _) => Reposition("載入完成");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                // 點膠囊時不要把焦點從使用者正在打字的視窗搶走
                handled = true;
                return new IntPtr(MA_NOACTIVATE);

            case WM_DPICHANGED:
                // 讓 WPF 先處理縮放,之後再依新的 DPI 重新計算位置
                _displays?.Schedule();
                break;

            case WM_DISPLAYCHANGE:
                _displays?.Schedule();
                break;
        }
        return IntPtr.Zero;
    }

    // ── 定位 ─────────────────────────────────────────────

    /// <summary>把視窗放到目標螢幕的上方中間。所有運算都用實體像素。</summary>
    private void Reposition(string reason)
    {
        if (_hwnd == IntPtr.Zero) return;

        var monitor = MonitorInfo.Select(_settings.Display);
        if (monitor is null)
        {
            Log.Write($"[{reason}] 找不到任何螢幕");
            return;
        }

        int w = (int)Math.Round(Width * monitor.Scale);
        int h = (int)Math.Round(Height * monitor.Scale);
        int x = monitor.Bounds.Left + (monitor.Bounds.Width - w) / 2;
        int y = monitor.Bounds.Top + (int)Math.Round(_settings.TopOffset * monitor.Scale);

        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        Log.Write($"[{reason}] 定位到 {monitor} → 視窗 ({x},{y}) {w}x{h}");
    }

    // ── 內容 ─────────────────────────────────────────────

    private void BuildCollapsedContent()
    {
        CenterSlot.Content = _modules
            .Select(m => SafeCreate(m, () => m.CreateCollapsedView()))
            .FirstOrDefault(v => v is not null);

        foreach (var m in _modules) m.BadgeChanged += (_, _) => RefreshBadges();
        RefreshBadges();
    }

    private void RefreshBadges()
    {
        BadgeStrip.Children.Clear();
        foreach (var badge in _modules.Select(m => m.Badge).OfType<IslandBadge>())
            BadgeStrip.Children.Add(CreateBadgeView(badge));
    }

    private UIElement CreateBadgeView(IslandBadge badge)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        var brush = (Brush)FindResource("SecondaryTextBrush");
        if (badge.Glyph is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = badge.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = badge.Text,
            FontSize = 11.5,
            Foreground = brush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return panel;
    }

    private static FrameworkElement? SafeCreate(IIslandModule m, Func<FrameworkElement?> create)
    {
        try { return create(); }
        catch (Exception ex)
        {
            Log.Write($"模組 {m.Id} 建立畫面失敗:{ex.Message}");
            return null;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}

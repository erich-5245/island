using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Island.App.Infrastructure;
using Island.App.Interop;
using Island.App.Modules;
using Island.Core;
using static Island.App.Interop.NativeMethods;

namespace Island.App;

public partial class MainWindow : Window, IShellController
{
    // ── 尺寸與動畫(DIP;尺寸來自 settings.json) ─────────
    // 三段:收合(只有時間)→ 滑鼠移過去「預覽」(變寬成一排字)→ 點擊「展開」
    private readonly Size _collapsedSize;
    private readonly Size _peekSize;
    private readonly Size _expandedSize;
    private const double ExpandedRadius = 24;

    private static readonly Duration PeekDuration = new(TimeSpan.FromMilliseconds(200));
    private static readonly Duration ExpandDuration = new(TimeSpan.FromMilliseconds(340));
    private static readonly Duration CollapseDuration = new(TimeSpan.FromMilliseconds(260));

    /// <summary>展開時每隔多久確認一次滑鼠是否還在島上(只在展開期間運作)。</summary>
    private static readonly TimeSpan LeaveCheckInterval = TimeSpan.FromMilliseconds(350);
    /// <summary>連續幾次確認滑鼠都不在島上才收合(≈ 0.7 秒)。</summary>
    private const int LeaveTicksToCollapse = 2;

    private static readonly Brush DragHighlightBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    private readonly IslandSettings _settings;
    private readonly IReadOnlyList<IIslandModule> _modules;
    private readonly Dictionary<IIslandModule, FrameworkElement> _expandedViews = [];
    private readonly Dictionary<IIslandModule, RadioButton> _tabs = [];
    private readonly DispatcherTimer _leaveTimer;
    private bool _expandedViewsBuilt;

    private IntPtr _hwnd;
    private TopmostKeeper? _topmost;
    private DisplayWatcher? _displays;

    private bool _expanded;
    private IIslandModule? _active;
    private int _holdCount;
    private int _outsideTicks;
    private bool _dragInside;

    internal MainWindow(IslandSettings settings, IReadOnlyList<IIslandModule> modules)
    {
        _settings = settings;
        _modules = modules;
        InitializeComponent();

        _collapsedSize = new Size(settings.CollapsedWidth, settings.CollapsedHeight);
        _peekSize = new Size(settings.PeekWidth, settings.CollapsedHeight);
        _expandedSize = new Size(settings.ExpandedWidth, settings.ExpandedHeight);

        Capsule.Width = _collapsedSize.Width;
        Capsule.Height = _collapsedSize.Height;
        Capsule.CornerRadius = new CornerRadius(_collapsedSize.Height / 2);
        CollapsedLayer.Height = _collapsedSize.Height - 2;              // 扣掉 1px 邊框
        ExpandedLayer.Width = _expandedSize.Width - 2;                  // 固定為展開尺寸,放大時像揭開
        ExpandedLayer.Height = _expandedSize.Height - 2;
        // 視窗要比展開後的島大一點(預留動畫回彈空間)
        Width = Math.Max(Width, Math.Max(_peekSize.Width, _expandedSize.Width) + 60);
        Height = Math.Max(Height, _expandedSize.Height + 60);

        _leaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LeaveCheckInterval };
        _leaveTimer.Tick += OnLeaveTimerTick;

        BuildCollapsedContent();
        Closed += (_, _) => { _leaveTimer.Stop(); _topmost?.Dispose(); _displays?.Dispose(); };
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
            case WM_DISPLAYCHANGE:
                // 讓 WPF 先處理縮放,之後再依新的 DPI 重新計算位置
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

    // ── 收合內容 ─────────────────────────────────────────

    private void BuildCollapsedContent()
    {
        CenterSlot.Content = _modules
            .Select(m => SafeCreate(m, () => m.CreateCollapsedView()))
            .FirstOrDefault(v => v is not null);

        LeftSlot.Content = _modules
            .OfType<IIslandPeekProvider>()
            .Select(p => SafeCreate((IIslandModule)p, p.CreatePeekView))
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

    // ── 展開內容與分頁 ───────────────────────────────────

    /// <summary>第一次展開時才建立各模組的展開畫面(之後重複使用)。</summary>
    private void EnsureExpandedViews()
    {
        if (_expandedViewsBuilt) return;
        _expandedViewsBuilt = true;

        foreach (var m in _modules)
        {
            var view = SafeCreate(m, () => m.CreateExpandedView());
            if (view is null) continue;
            _expandedViews[m] = view;

            var tab = new RadioButton
            {
                Content = m.Title,
                GroupName = "IslandTabs",
                Style = (Style)FindResource("TabButton"),
                Margin = new Thickness(2, 0, 2, 0),
            };
            tab.Checked += (_, _) => SwitchTo(m);
            _tabs[m] = tab;
            TabStrip.Children.Add(tab);
        }

        // 只有一個模組有展開畫面時不需要分頁
        TabStrip.Visibility = _tabs.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SwitchTo(IIslandModule module)
    {
        if (ReferenceEquals(_active, module) && ExpandedSlot.Content is not null) return;
        if (!_expandedViews.TryGetValue(module, out var view)) return;

        if (_expanded) NotifyExpand(_active, false);
        _active = module;
        ExpandedSlot.Content = view;
        if (_tabs.TryGetValue(module, out var tab) && tab.IsChecked != true) tab.IsChecked = true;
        if (_expanded) NotifyExpand(module, true);
    }

    // ── 展開 / 收合 ──────────────────────────────────────

    private void Expand(IIslandModule? module)
    {
        EnsureExpandedViews();
        module ??= _active ?? _expandedViews.Keys.FirstOrDefault();
        if (module is null || !_expandedViews.ContainsKey(module)) return;

        SwitchTo(module);
        _outsideTicks = 0;
        if (_expanded) return;

        _expanded = true;
        ExpandedLayer.Visibility = Visibility.Visible;
        Capsule.CornerRadius = new CornerRadius(ExpandedRadius);
        AnimateSize(_expandedSize, ExpandDuration, new BackEase { Amplitude = 0.2, EasingMode = EasingMode.EaseOut });
        Fade(CollapsedLayer, 0, TimeSpan.FromMilliseconds(90));
        Fade(ExpandedLayer, 1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(90));
        _leaveTimer.Start();
        NotifyExpand(module, true);
    }

    private void Collapse(bool force = false)
    {
        if (!_expanded) return;
        if (_holdCount > 0 && !force) return;

        _expanded = false;
        _leaveTimer.Stop();
        _dragInside = false;
        Capsule.BorderBrush = Brushes.Transparent;
        Capsule.CornerRadius = new CornerRadius(_collapsedSize.Height / 2);

        // 滑鼠還在島上 → 回到「預覽」一排字;否則回到收合
        bool peek = IsCursorOverCapsule();
        AnimateSize(peek ? _peekSize : _collapsedSize, CollapseDuration,
            new QuarticEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, peek ? 1 : 0, TimeSpan.FromMilliseconds(1));
        Fade(ExpandedLayer, 0, TimeSpan.FromMilliseconds(110), completed: () =>
        {
            if (!_expanded) ExpandedLayer.Visibility = Visibility.Collapsed;
        });
        Fade(CollapsedLayer, 1, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(110));
        NotifyExpand(_active, false);
    }

    /// <summary>展開期間每 350ms 用 GetCursorPos 確認滑鼠位置;收合後計時器停止,閒置時零成本。</summary>
    private void OnLeaveTimerTick(object? sender, EventArgs e)
    {
        bool stay = _holdCount > 0
                    || _dragInside
                    || Mouse.Captured is not null      // 右鍵選單等彈出視窗開啟中
                    || IsCursorOverCapsule();
        if (stay)
        {
            _outsideTicks = 0;
            return;
        }

        Capsule.BorderBrush = Brushes.Transparent;
        if (++_outsideTicks >= LeaveTicksToCollapse) Collapse();
    }

    private bool IsCursorOverCapsule()
    {
        if (!GetCursorPos(out var pt) || PresentationSource.FromVisual(Capsule) is null) return false;
        var p = Capsule.PointFromScreen(new Point(pt.X, pt.Y));
        const double slack = 6;
        return p.X >= -slack && p.Y >= -slack - 8   // 上方多留一點:膠囊上緣到螢幕頂端的縫隙
               && p.X <= Capsule.ActualWidth + slack && p.Y <= Capsule.ActualHeight + slack;
    }

    private void AnimateSize(Size to, Duration duration, IEasingFunction easing)
    {
        Capsule.BeginAnimation(WidthProperty, new DoubleAnimation(to.Width, duration) { EasingFunction = easing });
        Capsule.BeginAnimation(HeightProperty, new DoubleAnimation(to.Height, duration) { EasingFunction = easing });
    }

    private static void Fade(UIElement element, double to, TimeSpan duration, TimeSpan? delay = null, Action? completed = null)
    {
        var anim = new DoubleAnimation(to, new Duration(duration)) { BeginTime = delay ?? TimeSpan.Zero };
        if (completed is not null) anim.Completed += (_, _) => completed();
        element.BeginAnimation(OpacityProperty, anim);
    }

    private void NotifyExpand(IIslandModule? module, bool expanded)
    {
        if (module is not IIslandExpandAware aware) return;
        try
        {
            if (expanded) aware.OnExpanded(); else aware.OnCollapsed();
        }
        catch (Exception ex)
        {
            Log.Write($"模組 {module.Id} 展開/收合通知失敗:{ex.Message}");
        }
    }

    // ── 滑鼠 ─────────────────────────────────────────────

    // 滑鼠移過去:變寬成「一排字」(左側淡入日期等預覽內容);移開:回到收合。點擊才展開。

    private void OnCapsuleMouseEnter(object sender, MouseEventArgs e)
    {
        _outsideTicks = 0;
        if (_expanded) return;
        AnimateSize(_peekSize, PeekDuration, new CubicEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, 1, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60));
    }

    private void OnCapsuleMouseLeave(object sender, MouseEventArgs e)
    {
        if (_expanded) return;
        AnimateSize(_collapsedSize, PeekDuration, new CubicEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, 0, TimeSpan.FromMilliseconds(90));
    }

    private void OnCapsuleClick(object sender, MouseButtonEventArgs e)
    {
        if (_expanded) return;
        Expand(null);
        e.Handled = true;
    }

    // ── 拖放:交給第一個願意接收的模組 ───────────────────

    private (IIslandModule Module, IIslandDropTarget Target)? FindDropTarget(IDataObject data)
    {
        foreach (var m in _modules)
        {
            if (m is not IIslandDropTarget target) continue;
            try
            {
                if (target.CanAccept(data)) return (m, target);
            }
            catch (Exception ex)
            {
                Log.Write($"模組 {m.Id} CanAccept 失敗:{ex.Message}");
            }
        }
        return null;
    }

    private void OnCapsuleDragEnter(object sender, DragEventArgs e)
    {
        var found = FindDropTarget(e.Data);
        e.Effects = found is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        if (found is null) return;

        _dragInside = true;
        Capsule.BorderBrush = DragHighlightBrush;
        Expand(found.Value.Module);
    }

    private void OnCapsuleDragOver(object sender, DragEventArgs e)
    {
        var found = FindDropTarget(e.Data);
        e.Effects = found is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        if (found is null) return;

        _dragInside = true;
        _outsideTicks = 0;
        Capsule.BorderBrush = DragHighlightBrush;
        if (!_expanded || !ReferenceEquals(_active, found.Value.Module)) Expand(found.Value.Module);
    }

    private void OnCapsuleDragLeave(object sender, DragEventArgs e)
    {
        // 在子元素之間移動時也會觸發;真的離開與否交給 _leaveTimer 判斷
        _dragInside = false;
        e.Handled = true;
    }

    private void OnCapsuleDrop(object sender, DragEventArgs e)
    {
        _dragInside = false;
        Capsule.BorderBrush = Brushes.Transparent;
        e.Handled = true;

        var found = FindDropTarget(e.Data);
        if (found is null)
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        try
        {
            found.Value.Target.Accept(e.Data);
            e.Effects = DragDropEffects.Copy;
            Expand(found.Value.Module);
        }
        catch (Exception ex)
        {
            Log.Write($"模組 {found.Value.Module.Id} 接收拖放失敗:{ex}");
            e.Effects = DragDropEffects.None;
        }
    }

    // ── IShellController(模組透過 IIslandHost 呼叫) ─────

    void IShellController.Expand(string moduleId)
    {
        var m = _modules.FirstOrDefault(x => string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (m is not null) Expand(m);
    }

    void IShellController.Collapse() => Collapse();

    IDisposable IShellController.HoldExpanded()
    {
        _holdCount++;
        return new Hold(this);
    }

    private sealed class Hold(MainWindow owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._holdCount = Math.Max(0, owner._holdCount - 1);
            owner._outsideTicks = 0;
        }
    }

    // ── 其他 ─────────────────────────────────────────────

    private static FrameworkElement? SafeCreate(IIslandModule m, Func<FrameworkElement?> create)
    {
        try { return create(); }
        catch (Exception ex)
        {
            Log.Write($"模組 {m.Id} 建立畫面失敗:{ex.Message}");
            return null;
        }
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}

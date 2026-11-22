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
    // 三段:閒置(隱藏,或只有時間的小膠囊)→ 滑鼠移過去「預覽」(一排字)→ 點擊「固定展開」
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

    /// <summary>滑鼠離開後等多久才隱藏(避免在熱區與膠囊之間移動時閃爍)。</summary>
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(220);
    /// <summary>熱區往下多蓋住膠囊上緣幾 DIP,讓滑鼠從熱區往下移時不會中斷。</summary>
    private const double HotZoneOverlap = 4;

    private static readonly Brush DragHighlightBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    private readonly IslandSettings _settings;
    private readonly IReadOnlyList<IIslandModule> _modules;
    private readonly Dictionary<IIslandModule, FrameworkElement> _expandedViews = [];
    private readonly Dictionary<IIslandModule, RadioButton> _tabs = [];
    private readonly DispatcherTimer _leaveTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly OutsideClickWatcher _outsideClick;
    private bool _expandedViewsBuilt;

    private IntPtr _hwnd;
    private TopmostKeeper? _topmost;
    private DisplayWatcher? _displays;
    private FullscreenWatcher? _fullscreen;
    private IntPtr _monitor;

    private bool _expanded;
    private bool _pinned;       // 點擊展開 = 固定;滑鼠離開也不收,點外面才收
    private bool _peeking;
    private readonly Dictionary<IIslandModule, FrameworkElement?> _activityViews = [];
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

        // 視窗貼齊螢幕頂端,膠囊往下 TopOffset;中間的縫由熱區補滿
        Capsule.Margin = new Thickness(0, settings.TopOffset, 0, 0);
        HotZone.Width = _collapsedSize.Width;
        HotZone.Height = settings.TopOffset + HotZoneOverlap;
        ApplyIdleMode(animate: false);

        Capsule.Width = _collapsedSize.Width;
        Capsule.Height = _collapsedSize.Height;
        Capsule.CornerRadius = new CornerRadius(_collapsedSize.Height / 2);
        CollapsedLayer.Height = _collapsedSize.Height - 2;              // 扣掉 1px 邊框
        ExpandedLayer.Width = _expandedSize.Width - 2;                  // 固定為展開尺寸,放大時像揭開
        ExpandedLayer.Height = _expandedSize.Height - 2;
        // 視窗要比展開後的島大一點(預留動畫回彈空間)
        Width = Math.Max(Width, Math.Max(_peekSize.Width, _expandedSize.Width) + 60);
        Height = Math.Max(Height, _expandedSize.Height + settings.TopOffset + 60);

        _leaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LeaveCheckInterval };
        _leaveTimer.Tick += OnLeaveTimerTick;
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = HideDelay };
        _hideTimer.Tick += OnHideTimerTick;
        _outsideClick = new OutsideClickWatcher(Dispatcher, OnClickedOutside);
        IslandRoot.ContextMenu.Closed += (_, _) => _hideTimer.Start();

        BuildCollapsedContent();
        Closed += (_, _) => { _leaveTimer.Stop(); _hideTimer.Stop(); _outsideClick.Dispose(); _topmost?.Dispose(); _displays?.Dispose(); _fullscreen?.Dispose(); };
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

        _fullscreen = new FullscreenWatcher(_hwnd, Dispatcher, () => _monitor, OnFullscreenChanged);
        _topmost.ForegroundChanged += () => { if (_settings.HideInFullscreen) _fullscreen.Schedule(); };
        Loaded += (_, _) => Reposition("載入完成");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_fullscreen is not null && _fullscreen.HandleMessage(msg, wParam))
        {
            if (_settings.HideInFullscreen) handled = true;
            return IntPtr.Zero;
        }

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
        int y = monitor.Bounds.Top;   // TopOffset 由膠囊的 Margin 負責(熱區要貼齊螢幕上緣)

        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        Log.Write($"[{reason}] 定位到 {monitor} → 視窗 ({x},{y}) {w}x{h}");

        _monitor = monitor.Handle;
        if (_settings.HideInFullscreen) _fullscreen?.Schedule();
    }

    // ── 全螢幕時隱藏 ─────────────────────────────────────

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (!_settings.HideInFullscreen) fullscreen = false;
        Log.Write(fullscreen ? "偵測到全螢幕程式 → 隱藏" : "全螢幕結束 → 顯示");
        if (fullscreen)
        {
            Collapse(force: true);
            GoIdle();
            Hide();
        }
        else if (!IsVisible)
        {
            Show();                 // ShowActivated=false,不會搶焦點
            _topmost?.Reassert();
        }
    }

    // ── 右鍵選單 ─────────────────────────────────────────

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        AutoStartItem.IsChecked = AutoStart.IsEnabled;
        HideInFullscreenItem.IsChecked = _settings.HideInFullscreen;
        HideWhenIdleItem.IsChecked = _settings.HideWhenIdle;
        ShowActivityItem.IsChecked = _settings.ShowActivityWhenIdle;
    }

    private void OnShowActivityClick(object sender, RoutedEventArgs e)
    {
        _settings.ShowActivityWhenIdle = ShowActivityItem.IsChecked;
        _settings.Save();
        ApplyIdleMode(animate: true);
        UpdateCollapsedLayers();
    }

    private void OnHideWhenIdleClick(object sender, RoutedEventArgs e)
    {
        _settings.HideWhenIdle = HideWhenIdleItem.IsChecked;
        _settings.Save();
        ApplyIdleMode(animate: true);
    }

    private void OnAutoStartClick(object sender, RoutedEventArgs e)
    {
        try { AutoStart.Set(AutoStartItem.IsChecked); }
        catch (Exception ex)
        {
            Log.Write($"設定開機自動啟動失敗:{ex.Message}");
            AutoStartItem.IsChecked = AutoStart.IsEnabled;
        }
    }

    private void OnHideInFullscreenClick(object sender, RoutedEventArgs e)
    {
        _settings.HideInFullscreen = HideInFullscreenItem.IsChecked;
        _settings.Save();
        if (_settings.HideInFullscreen) _fullscreen?.Evaluate();
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

        foreach (var m in _modules)
            if (m is IIslandActivity a) a.ActivityChanged += (_, _) => OnActivityChanged();
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

    private void Expand(IIslandModule? module, bool pin = false)
    {
        EnsureExpandedViews();
        module ??= _active ?? _expandedViews.Keys.FirstOrDefault();
        if (module is null || !_expandedViews.ContainsKey(module)) return;

        SwitchTo(module);
        _outsideTicks = 0;
        _hideTimer.Stop();
        if (pin && !_pinned)
        {
            _pinned = true;
            _outsideClick.Start();
        }
        if (_expanded) return;

        _expanded = true;
        _peeking = false;
        Fade(Capsule, 1, TimeSpan.FromMilliseconds(120));
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
        _pinned = false;
        _outsideClick.Stop();
        _leaveTimer.Stop();
        _dragInside = false;
        Capsule.BorderBrush = Brushes.Transparent;
        Capsule.CornerRadius = new CornerRadius(_collapsedSize.Height / 2);

        // 滑鼠還在島上 → 回到「預覽」一排字;否則回到收合
        bool peek = IsCursorOverCapsule();
        _peeking = peek;
        AnimateSize(peek ? _peekSize : _collapsedSize, CollapseDuration,
            new QuarticEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, peek ? 1 : 0, TimeSpan.FromMilliseconds(1));
        if (!peek && !IdleVisible)
            Fade(Capsule, 0, TimeSpan.FromMilliseconds(220), TimeSpan.FromMilliseconds(60));
        UpdateCollapsedLayers();
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
        bool stay = _pinned
                    || _holdCount > 0
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

    // 閒置:隱藏(或小膠囊)。滑鼠碰到熱區/膠囊 → 預覽一排字;離開 → 回到閒置。
    // 點一下 → 固定展開(滑鼠移開也不收);點島以外任何地方才收合。

    private void OnIslandMouseEnter(object sender, MouseEventArgs e)
    {
        _outsideTicks = 0;
        _hideTimer.Stop();
        if (_expanded || _peeking) return;

        _peeking = true;
        UpdateCollapsedLayers();
        Fade(Capsule, 1, TimeSpan.FromMilliseconds(140));
        AnimateSize(_peekSize, PeekDuration, new CubicEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, 1, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60));
    }

    private void OnIslandMouseLeave(object sender, MouseEventArgs e)
    {
        if (_expanded) return;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (_expanded || IslandRoot.ContextMenu.IsOpen || IsCursorOverCapsule()) return;
        GoIdle();
    }

    /// <summary>回到閒置:縮回收合大小;「閒置時隱藏」開啟且沒有活動時整個淡出(只剩看不見的熱區)。</summary>
    private void GoIdle()
    {
        _peeking = false;
        AnimateSize(_collapsedSize, PeekDuration, new CubicEase { EasingMode = EasingMode.EaseOut });
        Fade(LeftSlot, 0, TimeSpan.FromMilliseconds(90));
        Fade(Capsule, IdleVisible ? 1 : 0, TimeSpan.FromMilliseconds(200));
        UpdateCollapsedLayers();
    }

    // ── 活動(例如正在播放):閒置時也顯示膠囊,內容換成活動畫面 ──

    /// <summary>第一個有活動的模組(依模組順序)。</summary>
    private IIslandModule? CurrentActivity =>
        _settings.ShowActivityWhenIdle
            ? _modules.FirstOrDefault(m => m is IIslandActivity { IsActivityActive: true })
            : null;

    /// <summary>閒置時膠囊是否可見。</summary>
    private bool IdleVisible => !_settings.HideWhenIdle || CurrentActivity is not null;

    private void OnActivityChanged()
    {
        Log.Write($"活動:{CurrentActivity?.Id ?? "(無)"}");
        if (_expanded || _peeking) return;
        Fade(Capsule, IdleVisible ? 1 : 0, TimeSpan.FromMilliseconds(220));
        UpdateCollapsedLayers();
    }

    /// <summary>收合層顯示「時間 + 小提示」還是「活動畫面」。預覽(一排字)時一律顯示前者。</summary>
    private void UpdateCollapsedLayers()
    {
        var activity = _peeking || _expanded ? null : CurrentActivity;
        if (activity is not null)
        {
            if (!_activityViews.TryGetValue(activity, out var view))
            {
                view = SafeCreate(activity, ((IIslandActivity)activity).CreateActivityView);
                _activityViews[activity] = view;
            }
            if (view is null) activity = null;
            else if (!ReferenceEquals(ActivitySlot.Content, view)) ActivitySlot.Content = view;
        }

        bool showActivity = activity is not null;
        var d = TimeSpan.FromMilliseconds(160);
        Fade(ActivitySlot, showActivity ? 1 : 0, d);
        Fade(CenterSlot, showActivity ? 0 : 1, d);
        Fade(BadgeStrip, showActivity ? 0 : 1, d);
    }

    private void ApplyIdleMode(bool animate)
    {
        bool hidden = !IdleVisible && !_expanded && !_peeking;
        if (animate)
        {
            Fade(Capsule, hidden ? 0 : 1, TimeSpan.FromMilliseconds(200));
            return;
        }
        Capsule.BeginAnimation(OpacityProperty, null);
        Capsule.Opacity = hidden ? 0 : 1;
    }

    private void OnCapsuleClick(object sender, MouseButtonEventArgs e)
    {
        if (_expanded && _pinned) return;   // 已固定:點裡面是在操作模組內容
        // 有活動(例如正在播放)時直接展開到那個模組
        var activity = _modules.FirstOrDefault(m => m is IIslandActivity { IsActivityActive: true });
        Expand(activity, pin: true);
        e.Handled = true;
    }

    private void OnClickedOutside()
    {
        if (!_pinned || _holdCount > 0) return;
        Collapse();
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

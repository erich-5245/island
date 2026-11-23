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
    private Size _collapsedSize;
    private Size _peekSize;
    private Size _expandedSize;
    /// <summary>圓角跟著主題(圓角程度)。</summary>
    private double ExpandedRadius => _settings.Theme.ExpandedRadius;
    private double CollapsedRadius => _settings.Theme.CollapsedRadius(_collapsedSize.Height);

    /// <summary>動畫時間倍率(主題的「動畫速度」)。</summary>
    private static double s_animScale = 1;
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms * s_animScale);

    private static Duration PeekDuration => new(Ms(200));
    private static Duration ExpandDuration => new(Ms(340));
    private static Duration CollapseDuration => new(Ms(260));

    /// <summary>展開時每隔多久確認一次滑鼠是否還在島上(只在展開期間運作)。</summary>
    private static readonly TimeSpan LeaveCheckInterval = TimeSpan.FromMilliseconds(350);
    /// <summary>連續幾次確認滑鼠都不在島上才收合(≈ 0.7 秒)。</summary>
    private const int LeaveTicksToCollapse = 2;

    /// <summary>滑鼠離開後等多久才隱藏(避免在熱區與膠囊之間移動時閃爍)。</summary>
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(220);
    /// <summary>
    /// 熱區高度(DIP):只有螢幕最上緣一條細線,滑鼠推到頂端一定碰得到,
    /// 又幾乎不會擋到瀏覽器分頁等點擊。熱區到膠囊之間的縫由 IsCursorOverCapsule 的寬容範圍處理。
    /// </summary>
    private const double HotZoneHeight = 4;

    /// <summary>熱區寬度 = 整排的寬度(兩側小圓也算),小圓上方也叫得出來 / 不會被當成離開。</summary>
    private double HotZoneBaseWidth => _peekSize.Width + 2 * (_collapsedSize.Height + 6);

    private const int HotkeyId = 1;
    private const int EscHotkeyId = 2;
    /// <summary>Ctrl+Alt+1…9 的熱鍵 id = 100 + 數字。</summary>
    private const int NumberHotkeyBase = 100;
    private bool _numberHotkeysRegistered;
    private bool _escRegistered;

    private static readonly Brush DragHighlightBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    private readonly IslandSettings _settings;
    private readonly IReadOnlyList<IIslandModule> _modules;
    private readonly Dictionary<IIslandModule, FrameworkElement> _expandedViews = [];
    private readonly Dictionary<IIslandModule, RadioButton> _tabs = [];
    private readonly DispatcherTimer _leaveTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly OutsideClickWatcher _outsideClick;
    /// <summary>
    /// 右鍵選單開著時監看「點外面」。島不取得焦點,WPF 自己偵測不到點到別的程式,
    /// 選單會一直留在畫面上,所以由我們在點外面時關掉它(島的選單與 Shelf 檔案選單都適用)。
    /// </summary>
    private readonly OutsideClickWatcher _menuOutsideClick;
    private readonly HashSet<ContextMenu> _openMenus = [];
    private bool _expandedViewsBuilt;

    private IntPtr _hwnd;
    private TopmostKeeper? _topmost;
    private DisplayWatcher? _displays;
    private FullscreenWatcher? _fullscreen;
    private IntPtr _monitor;

    private readonly DispatcherTimer _announceTimer;
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

        IslandRow.RenderTransform = _slide;   // 隱藏 / 出現:整列往上滑出螢幕、從上方滑下來
        ApplyLayout();
        ApplyStartButton();
        Capsule.Width = _collapsedSize.Width;
        Capsule.Height = _collapsedSize.Height;
        Capsule.CornerRadius = new CornerRadius(CollapsedRadius);
        // 視窗大小跟著狀態:平常只放得下一排 + 小圓,展開時才放大(見 UseBigWindow)
        _shrinkTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(450) };
        _shrinkTimer.Tick += OnShrinkTimerTick;
        UseBigWindow(false);
        ApplyTheme(colors: false);   // 顏色已在 App 啟動時由 AccentTheme.Install 放好

        // 初始狀態:閒置時隱藏 → Hidden;否則 Idle(小膠囊)
        _surface = IdleVisible ? Surface.Idle : Surface.Hidden;
        Capsule.Opacity = IdleVisible ? 1 : 0;
        Capsule.Visibility = IdleVisible ? Visibility.Visible : Visibility.Hidden;

        _leaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LeaveCheckInterval };
        _leaveTimer.Tick += OnLeaveTimerTick;
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = HideDelay };
        _hideTimer.Tick += OnHideTimerTick;
        _outsideClick = new OutsideClickWatcher(Dispatcher, OnClickedOutside);
        _announceTimer = new DispatcherTimer(DispatcherPriority.Background);
        _announceTimer.Tick += OnAnnounceTimerTick;
        IslandRoot.ContextMenu.Closed += (_, _) => _hideTimer.Start();
        _menuOutsideClick = new OutsideClickWatcher(Dispatcher, CloseMenus);
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnAnyMenuOpened));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler(OnAnyMenuClosed));

        BuildCollapsedContent();
        Closed += (_, _) => { _leaveTimer.Stop(); _hideTimer.Stop(); _announceTimer.Stop(); _shrinkTimer.Stop(); _outsideClick.Dispose(); _menuOutsideClick.Dispose(); _topmost?.Dispose(); _displays?.Dispose(); _fullscreen?.Dispose(); };
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

        ApplyHotkey();
        ApplyNumberHotkeys();
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

            case GlobalHotkey.WM_HOTKEY:
                if (wParam.ToInt32() == HotkeyId) OnHotkey();
                else if (wParam.ToInt32() is >= NumberHotkeyBase + 1 and <= NumberHotkeyBase + 9) OnNumberHotkey(wParam.ToInt32() - NumberHotkeyBase);
                else if (wParam.ToInt32() == EscHotkeyId && IsExpanded) CollapseExpanded(force: true);
                handled = true;
                break;

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
    private void Reposition(string? reason)
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
        if (reason is null) return;   // 只是換視窗大小
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
            _announced = null;
            _announceTimer.Stop();
            HideSurface(instant: true);
            Hide();
        }
        else if (!IsVisible)
        {
            Show();                 // ShowActivated=false,不會搶焦點
            _topmost?.Reassert();
            ApplyIdleMode();        // 「閒置時隱藏」關閉時,小膠囊回來
        }
    }

    // ── 右鍵選單 ─────────────────────────────────────────

    private void OnAnyMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        _openMenus.Add(menu);
        _menuOutsideClick.Start();
    }

    private void OnAnyMenuClosed(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu) _openMenus.Remove(menu);
        if (_openMenus.Count == 0) _menuOutsideClick.Stop();
        // 延伸列上的「+N」選單關掉了,滑鼠也不在小圓上 → 收起延伸列
        if (_openMenus.Count == 0 && _stripCircle is not null && GetCursorPos(out var pt) && !IsCursorOver(_stripCircle, pt)) CloseStrip();
    }

    /// <summary>關掉所有開著的右鍵選單(點到外面、島要隱藏時)。</summary>
    private void CloseMenus()
    {
        foreach (var menu in _openMenus.ToArray()) menu.IsOpen = false;
        _openMenus.Clear();
        _menuOutsideClick.Stop();
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        AutoStartItem.IsChecked = AutoStart.IsEnabled;
        HideInFullscreenItem.IsChecked = _settings.HideInFullscreen;
        HideWhenIdleItem.IsChecked = _settings.HideWhenIdle;
        AnnounceItem.IsChecked = _settings.AnnounceActivity;
        OsdItem.IsChecked = _settings.ShowOsd;
    }

    private void OnOsdClick(object sender, RoutedEventArgs e)
    {
        _settings.ShowOsd = OsdItem.IsChecked;
        _settings.Save();
    }

    private void OnAnnounceClick(object sender, RoutedEventArgs e)
    {
        _settings.AnnounceActivity = AnnounceItem.IsChecked;
        _settings.Save();
    }

    private void OnHideWhenIdleClick(object sender, RoutedEventArgs e)
    {
        _settings.HideWhenIdle = HideWhenIdleItem.IsChecked;
        _settings.Save();
        ApplyIdleMode();
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

        foreach (var m in _modules) m.BadgeChanged += (_, _) => { RefreshBadges(); UpdateCircles(); };
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
            // 拖曳經過分頁標籤時切到那個分頁(例如把程式拖到「捷徑」)
            tab.AllowDrop = true;
            tab.DragEnter += (_, _) => SwitchTo(m);
            _tabs[m] = tab;
            TabStrip.Children.Add(tab);
        }

        // 只有一個模組有展開畫面時不需要分頁
        TabStrip.Visibility = _tabs.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(UpdateTabRow, DispatcherPriority.Loaded);
    }

    private void SwitchTo(IIslandModule module)
    {
        if (ReferenceEquals(_active, module) && ExpandedSlot.Content is not null) return;
        if (!_expandedViews.TryGetValue(module, out var view)) return;

        if (IsExpanded) NotifyExpand(_active, false);
        _active = module;
        ExpandedSlot.Content = view;
        if (_tabs.TryGetValue(module, out var tab))
        {
            if (tab.IsChecked != true) tab.IsChecked = true;
            if (IsExpanded) ScrollActiveTabIntoView();   // 分頁太多被捲到外面時,捲回看得到
        }
        if (IsExpanded) NotifyExpand(module, true);
    }

    // ── 狀態 ─────────────────────────────────────────────
    //
    //   Hidden ──滑鼠碰到熱區──▶ Peek ──點一下──▶ Expanded
    //     ▲  ▲                    │  ▲                │
    //     │  └──提示結束── Announce ◀┘  └──點外面/滑鼠離開(暫時展開)──┘
    //     └────────── 滑鼠離開 / 收起 ─────────────────────────────┘
    //   (「閒置時隱藏」關閉時,Hidden 改成 Idle:平常顯示只有時間的小膠囊)
    //
    // 所有轉換都經過 ShowSurface / HideSurface / ToExpanded / CollapseExpanded,
    // 畫面內容(一排裡顯示什麼、小圓)由 UpdateContent 依狀態推導,不另外記旗標。

    private enum Surface { Hidden, Idle, Peek, Announce, Expanded }

    private Surface _surface = Surface.Hidden;
    private bool _pinned;                 // 點擊展開 = 固定;滑鼠離開也不收,點外面才收
    private bool _leaving;                // 正在往上滑出(狀態已經是 Hidden)
    private IIslandModule? _announced;    // 提示中的模組(Announce 或 Peek 時有效)

    private bool IsExpanded => _surface == Surface.Expanded;

    /// <summary>閒置時膠囊是否可見(「閒置時隱藏」關閉)。</summary>
    private bool IdleVisible => !_settings.HideWhenIdle;

    /// <summary>回到「沒事」的狀態:Idle(小膠囊)或 Hidden。</summary>
    private void ToRest()
    {
        if (IdleVisible) ShowSurface(Surface.Idle);
        else HideSurface();
    }

    /// <summary>顯示成一排(Peek / Announce)或小膠囊(Idle)。</summary>
    private void ShowSurface(Surface target)
    {
        var from = _surface;
        bool wasHidden = from == Surface.Hidden && !_leaving;
        bool wasLeaving = _leaving;
        if (from == Surface.Expanded) LeaveExpanded();
        _surface = target;
        _leaving = false;
        _hideTimer.Stop();
        UpdateNotch();
        var size = target == Surface.Idle ? _collapsedSize : _peekSize;

        Capsule.BeginAnimation(OpacityProperty, null);
        Capsule.Opacity = 1;
        Capsule.Visibility = Visibility.Visible;

        if (wasHidden)
        {
            // 從隱藏出現:內容先換好、直接用目標大小,從螢幕上方滑下來
            SetOpacity(CollapsedLayer, 1);
            UpdateContent(instant: true);
            SetSize(size);
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.Y = OffscreenY;
            SlideDown();
            return;
        }

        if (wasLeaving) SlideDown();   // 滑出途中被叫回來

        if (from == Surface.Expanded || ExpandedLayer.Visibility == Visibility.Visible)
        {
            ShrinkPanel(size);          // 展開的面板縮回一排
        }
        else
        {
            SetOpacity(CollapsedLayer, 1);
            AnimateSize(size, PeekDuration, new CubicEase { EasingMode = EasingMode.EaseOut });
            UpdateContent();
        }
    }

    /// <summary>隱藏:整列維持原樣往上滑出螢幕,看不見後才重設(instant = 不要動畫)。</summary>
    private void HideSurface(bool instant = false)
    {
        if (_surface == Surface.Hidden && !instant) return;
        if (_surface == Surface.Expanded) LeaveExpanded();
        _surface = Surface.Hidden;
        _hideTimer.Stop();
        CloseMenus();
        if (instant)
        {
            _leaving = false;
            ResetHidden();
            return;
        }
        _leaving = true;
        var anim = new DoubleAnimation(OffscreenY, SlideOutDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        anim.Completed += (_, _) =>
        {
            if (!_leaving) return;   // 滑到一半又被叫出來
            _leaving = false;
            ResetHidden();
        };
        _slide.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private void ToExpanded(IIslandModule? module, bool pin)
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
        if (IsExpanded) return;

        bool wasLeaving = _leaving;
        _surface = Surface.Expanded;
        _leaving = false;
        _announced = null;
        _announceTimer.Stop();
        UseBigWindow(true);
        if (wasLeaving) SlideDown();

        ApplyCapsuleChrome();
        UpdateCircles();
        Fade(Capsule, 1, Ms(120));
        ExpandedLayer.Visibility = Visibility.Visible;
        Capsule.CornerRadius = new CornerRadius(ExpandedRadius);
        AnimateSize(_expandedSize, ExpandDuration, new BackEase { Amplitude = 0.2, EasingMode = EasingMode.EaseOut });
        Fade(CollapsedLayer, 0, Ms(90));
        Fade(ExpandedLayer, 1, Ms(200), Ms(90));
        _leaveTimer.Start();
        NotifyExpand(module, true);
        // 面板剛變成看得見,版面排好後才捲得動:把目前的分頁捲回看得到的地方
        Dispatcher.BeginInvoke(ScrollActiveTabIntoView, DispatcherPriority.Loaded);
    }

    private void ScrollActiveTabIntoView()
    {
        if (_active is null || !_tabs.TryGetValue(_active, out var tab) || !tab.IsVisible) return;
        if (TabScroller.ExtentWidth <= TabScroller.ViewportWidth + 1)
        {
            TabScroller.ScrollToHorizontalOffset(0);
            return;
        }
        var left = tab.TransformToAncestor(TabStrip).Transform(new Point(0, 0)).X;
        double offset = TabScroller.HorizontalOffset;
        if (left < offset) offset = left - 8;
        else if (left + tab.ActualWidth > offset + TabScroller.ViewportWidth) offset = left + tab.ActualWidth - TabScroller.ViewportWidth + 8;
        TabScroller.ScrollToHorizontalOffset(Math.Max(0, offset));
    }

    /// <summary>收起展開的面板:滑鼠還在島上 → 一排;否則回到沒事的狀態。</summary>
    private void CollapseExpanded(bool force = false)
    {
        if (!IsExpanded) return;
        if (_holdCount > 0 && !force) return;
        if (IsCursorOverCapsule()) ShowSurface(Surface.Peek);
        else ToRest();
    }

    /// <summary>離開 Expanded 時的共同收尾(由 ShowSurface / HideSurface 呼叫)。</summary>
    private void LeaveExpanded()
    {
        if (_escRegistered)
        {
            GlobalHotkey.Unregister(_hwnd, EscHotkeyId);
            _escRegistered = false;
        }
        _pinned = false;
        _outsideClick.Stop();
        _leaveTimer.Stop();
        _dragInside = false;
        NotifyExpand(_active, false);
        ScheduleShrink();
    }

    /// <summary>展開面板縮回一排(或小膠囊),島仍然看得到時使用。</summary>
    private void ShrinkPanel(Size to)
    {
        ApplyCapsuleChrome();
        Capsule.CornerRadius = new CornerRadius(CollapsedRadius);
        AnimateSize(to, CollapseDuration, new QuarticEase { EasingMode = EasingMode.EaseOut });
        Fade(ExpandedLayer, 0, Ms(110), completed: () =>
        {
            if (!IsExpanded) ExpandedLayer.Visibility = Visibility.Collapsed;
        });
        UpdateContent();
        Fade(CollapsedLayer, 1, Ms(160), Ms(110));
    }

    // ── 滑出 / 滑入 ──────────────────────────────────────

    private readonly TranslateTransform _slide = new();
    private static Duration SlideOutDuration => new(Ms(230));
    private static Duration SlideInDuration => new(Ms(320));

    /// <summary>剛好完全離開螢幕上緣的位移(依目前高度)。</summary>
    private double OffscreenY => -(_settings.TopOffset + Math.Max(Capsule.ActualHeight, IslandRow.ActualHeight) + 6);

    private void SlideDown() =>
        _slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, SlideInDuration) { EasingFunction = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut } });

    /// <summary>已經看不見了:把膠囊重設成收合狀態、位移歸零、視窗縮小,等下次滑入。</summary>
    private void ResetHidden()
    {
        CloseStrip(instant: true);
        UpdateNotch();
        foreach (var (circle, slot) in new[] { (LeftCircle, LeftCircleSlot), (RightCircle, RightCircleSlot) })
        {
            circle.Tag = null;
            circle.BeginAnimation(OpacityProperty, null);
            circle.Opacity = 0;
            circle.Visibility = Visibility.Collapsed;
            slot.Content = null;
        }
        Capsule.BeginAnimation(OpacityProperty, null);
        Capsule.Opacity = 0;
        Capsule.Visibility = Visibility.Hidden;
        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        _slide.Y = 0;
        ExpandedLayer.BeginAnimation(OpacityProperty, null);
        ExpandedLayer.Opacity = 0;
        ExpandedLayer.Visibility = Visibility.Collapsed;
        ApplyCapsuleChrome();
        Capsule.CornerRadius = new CornerRadius(CollapsedRadius);
        SetSize(_collapsedSize);
        UpdateContent(instant: true);
        UseBigWindow(false, force: true);   // 看不見時才換大小(例如延伸列的預留空間改變了)
    }

    private void SetSize(Size size)
    {
        Capsule.BeginAnimation(WidthProperty, null);
        Capsule.BeginAnimation(HeightProperty, null);
        Capsule.Width = size.Width;
        Capsule.Height = size.Height;
    }

    // ── 視窗大小跟著狀態 ─────────────────────────────────
    // 透明視窗每畫一格都要把整個視窗送給系統(軟體算圖),視窗越大動畫越耗 CPU。
    // 平常(隱藏 / 一排 / 提示)只用剛好放得下一排 + 小圓的大小;只有展開時才放大,收起後再縮回。

    private bool _bigWindow = true;   // 建構時先縮小
    private readonly DispatcherTimer _shrinkTimer;

    private double CompactWindowWidth => Math.Max(_peekSize.Width + 2 * (_collapsedSize.Height + 6), _collapsedSize.Width) + 24 + 2 * StripReserve;
    private double CompactWindowHeight => _settings.TopOffset + _collapsedSize.Height + 16;
    private double BigWindowWidth => Math.Max(CompactWindowWidth, _expandedSize.Width * 1.08 + 24);
    private double BigWindowHeight => _settings.TopOffset + _expandedSize.Height * 1.08 + 24;

    private void UseBigWindow(bool big, bool force = false)
    {
        _shrinkTimer.Stop();
        if (_bigWindow == big && !force) return;
        _bigWindow = big;
        Width = big ? BigWindowWidth : CompactWindowWidth;
        Height = big ? BigWindowHeight : CompactWindowHeight;
        Reposition(null);
    }

    /// <summary>依設定套用各部位的尺寸(啟動時,以及設定視窗改了尺寸時)。</summary>
    private void ApplyLayout()
    {
        _collapsedSize = new Size(_settings.CollapsedWidth, _settings.CollapsedHeight);
        _peekSize = new Size(_settings.PeekWidth, _settings.CollapsedHeight);
        _expandedSize = new Size(_settings.ExpandedWidth, _settings.ExpandedHeight);

        // 視窗貼齊螢幕頂端,膠囊往下 TopOffset
        IslandRow.Margin = new Thickness(0, _settings.TopOffset, 0, 0);
        foreach (var circle in new[] { LeftCircle, RightCircle })
        {
            circle.Width = circle.Height = _collapsedSize.Height;
            circle.CornerRadius = new CornerRadius(CollapsedRadius);
        }
        HotZone.Width = HotZoneBaseWidth;   // 和整排(小圓 + 長條 + 小圓)一樣寬,比較好叫出來
        HotZone.Height = HotZoneHeight;
        CollapsedLayer.Height = _collapsedSize.Height - 2;              // 扣掉 1px 邊框
        ExpandedLayer.Width = _expandedSize.Width - 2;                  // 固定為展開尺寸,放大時像揭開
        ExpandedLayer.Height = _expandedSize.Height - 2;
    }

    // ── 快捷鍵 ──────────────────────────────────────────

    /// <summary>註冊設定裡的快捷鍵;回傳是否成功(失敗通常是被其他程式佔用)。</summary>
    internal bool ApplyHotkey()
    {
        if (_hwnd == IntPtr.Zero) return false;
        if (string.IsNullOrWhiteSpace(_settings.Hotkey))
        {
            GlobalHotkey.Unregister(_hwnd, HotkeyId);
            return true;
        }
        bool ok = GlobalHotkey.Register(_hwnd, HotkeyId, _settings.Hotkey);
        Log.Write(ok ? $"快捷鍵:{_settings.Hotkey}" : $"快捷鍵 {_settings.Hotkey} 註冊失敗(格式不對或已被其他程式使用)");
        return ok;
    }

    /// <summary>快捷鍵:收著 → 固定展開(Esc 或再按一次收起);展開中 → 收起。</summary>
    /// <summary>Ctrl+Alt+1…9:交給實作 IIslandNumberHotkeys 的模組(捷徑)。設定可以關掉。</summary>
    internal void ApplyNumberHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_numberHotkeysRegistered)
        {
            for (int n = 1; n <= 9; n++) GlobalHotkey.Unregister(_hwnd, NumberHotkeyBase + n);
            _numberHotkeysRegistered = false;
        }
        if (!_settings.NumberHotkeys || !_modules.OfType<IIslandNumberHotkeys>().Any()) return;
        var failed = new List<int>();
        for (int n = 1; n <= 9; n++)
            if (!GlobalHotkey.Register(_hwnd, NumberHotkeyBase + n, $"Ctrl+Alt+D{n}")) failed.Add(n);
        _numberHotkeysRegistered = true;
        Log.Write(failed.Count == 0 ? "快捷鍵:Ctrl+Alt+1…9(捷徑)" : $"快捷鍵:Ctrl+Alt+1…9,其中 {string.Join(",", failed)} 被其他程式佔用");
    }

    private void OnNumberHotkey(int n)
    {
        var target = _modules.OfType<IIslandNumberHotkeys>().FirstOrDefault();
        try { target?.OnNumberHotkey(n); }
        catch (Exception ex) { Log.Write($"Ctrl+Alt+{n} 失敗:{ex.Message}"); }
    }

    private void OnHotkey()
    {
        if (!IsVisible) return;   // 全螢幕隱藏中
        if (IsExpanded)
        {
            CollapseExpanded(force: true);
            return;
        }
        ToExpanded(CurrentActivity, pin: true);
        if (IsExpanded) _escRegistered = GlobalHotkey.RegisterKey(_hwnd, EscHotkeyId, Key.Escape);
    }

    /// <summary>收起動畫結束後再縮小視窗(動畫進行中縮小會把面板切掉)。</summary>
    private void ScheduleShrink()
    {
        _shrinkTimer.Stop();
        _shrinkTimer.Start();
    }

    private void OnShrinkTimerTick(object? sender, EventArgs e)
    {
        _shrinkTimer.Stop();
        if (!IsExpanded && !_leaving && ExpandedLayer.Visibility != Visibility.Visible) UseBigWindow(false);
        else if (!IsExpanded) _shrinkTimer.Start();   // 還在收,等一下再試
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

        ApplyCapsuleChrome();
        if (++_outsideTicks >= LeaveTicksToCollapse) CollapseExpanded();
    }

    /// <summary>
    /// 膠囊的底色與邊框:展開時用面板色(有強調色時帶一點色調),收合/預覽時是黑色 + 膠囊邊框。
    /// 都是 DynamicResource,強調色變化時自動漸變。
    /// </summary>
    private void ApplyCapsuleChrome()
    {
        if (IsExpanded)
        {
            Capsule.SetResourceReference(Border.BackgroundProperty, IslandTheme.PanelBackground);
            Capsule.SetResourceReference(Border.BorderBrushProperty, IslandTheme.PanelBorder);
        }
        else
        {
            Capsule.SetResourceReference(Border.BackgroundProperty, "CapsuleBrush");
            Capsule.SetResourceReference(Border.BorderBrushProperty, IslandTheme.CapsuleBorder);
        }
    }

    /// <summary>滑鼠是否在島上(膠囊或兩側看得到的小圓)。</summary>
    private bool IsCursorOverCapsule()
    {
        if (!GetCursorPos(out var pt)) return false;
        return IsCursorOver(Capsule, pt)
               || (LeftCircle.IsVisible && IsCursorOver(LeftCircle, pt))
               || (RightCircle.IsVisible && IsCursorOver(RightCircle, pt));
    }

    private bool IsCursorOver(FrameworkElement element, POINT pt)
    {
        if (PresentationSource.FromVisual(element) is null) return false;
        var p = element.PointFromScreen(new Point(pt.X, pt.Y));
        const double slack = 6;
        // 上方一路算到螢幕頂端:膠囊 / 小圓上緣和螢幕之間的縫隙也算在島上
        return p.X >= -slack && p.Y >= -(_settings.TopOffset + slack + 8)
               && p.X <= element.ActualWidth + slack && p.Y <= element.ActualHeight + slack;
    }

    private void AnimateSize(Size to, Duration duration, IEasingFunction easing)
    {
        Capsule.BeginAnimation(WidthProperty, new DoubleAnimation(to.Width, duration) { EasingFunction = easing });
        Capsule.BeginAnimation(HeightProperty, new DoubleAnimation(to.Height, duration) { EasingFunction = easing });
    }

    private void Fade(UIElement element, double to, TimeSpan duration, TimeSpan? delay = null, Action? completed = null)
    {
        // 膠囊完全淡出後設為 Hidden:WPF 不再算圖,模組(例如計時器每秒更新)也能用 IsVisible 判斷要不要更新畫面
        bool capsule = ReferenceEquals(element, Capsule);
        if (capsule && to > 0) Capsule.Visibility = Visibility.Visible;

        var anim = new DoubleAnimation(to, new Duration(duration)) { BeginTime = delay ?? TimeSpan.Zero };
        anim.Completed += (_, _) =>
        {
            if (capsule && to <= 0 && Capsule.Opacity <= 0) Capsule.Visibility = Visibility.Hidden;
            completed?.Invoke();
        };
        element.BeginAnimation(OpacityProperty, anim);
    }

    private static void SetOpacity(UIElement element, double value)
    {
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = value;
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

    private void OnIslandMouseEnter(object sender, MouseEventArgs e)
    {
        _outsideTicks = 0;
        _hideTimer.Stop();
        if (_surface is Surface.Expanded or Surface.Peek) return;
        ShowSurface(Surface.Peek);   // 提示中移上來:提示內容保留到提示時間結束
    }

    private void OnIslandMouseLeave(object sender, MouseEventArgs e)
    {
        if (IsExpanded) return;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (_surface != Surface.Peek) return;
        if (IslandRoot.ContextMenu.IsOpen || IsCursorOverCapsule())
        {
            _hideTimer.Start();   // 還在島上(例如小圓上方的空隙):稍後再確認,離開後才收起
            return;
        }
        if (_announced is not null) ShowSurface(Surface.Announce);   // 提示還沒結束,繼續顯示提示
        else ToRest();
    }

    private void OnCapsuleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsExpanded && _pinned) return;   // 已固定:點裡面是在操作模組內容
        // 有活動(例如正在播放)時直接展開到那個模組
        ToExpanded(CurrentActivity, pin: true);
        e.Handled = true;
    }

    private void OnClickedOutside()
    {
        if (!_pinned || _holdCount > 0) return;
        CollapseExpanded();
    }

    // ── 活動(例如正在播放)與提示 ─────────────────────────

    /// <summary>依 liveOrder 排序的模組(沒列出的照模組順序排在後面)。</summary>
    private IEnumerable<IIslandModule> LiveOrdered =>
        _modules.OrderBy(m =>
        {
            int i = _settings.LiveOrder.FindIndex(id => string.Equals(id, m.Id, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        });

    /// <summary>中間長條要顯示的活動:專注中的模組優先,其次依 liveOrder 第一個有活動的模組。</summary>
    private IIslandModule? CurrentActivity =>
        FocusModule ?? LiveOrdered.FirstOrDefault(m => m is IIslandActivity { IsActivityActive: true });

    /// <summary>正在專注的模組(番茄鐘專注中);沒有就是 null。</summary>
    private IIslandModule? FocusModule =>
        _modules.FirstOrDefault(m => m is IIslandFocus f && SafeFocusing(f) && m is IIslandActivity);

    private static bool SafeFocusing(IIslandFocus f)
    {
        try { return f.IsFocusing; } catch { return false; }
    }

    private void OnActivityChanged()
    {
        Log.Write($"活動:{CurrentActivity?.Id ?? "(無)"}");
        if (_surface is Surface.Peek or Surface.Announce) UpdateContent();
        else UpdateCircles();
    }

    void IShellController.Announce(string moduleId, TimeSpan duration)
    {
        if (IsExpanded || !IsVisible) return;
        // 右鍵選單的開關:「切歌時顯示」只管音樂,「音量亮度提示」只管提示模組
        if (!_settings.AnnounceActivity && moduleId == "media") return;
        if (!_settings.ShowOsd && moduleId == "osd") return;
        var module = _modules.FirstOrDefault(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (module is not IIslandActivity) return;
        // 專注模式:只有專注中的模組自己和音量亮度提示可以跳出來
        if (FocusModule is { } focus && !ReferenceEquals(focus, module) && moduleId != "osd") return;

        _announced = module;
        _announceTimer.Stop();
        _announceTimer.Interval = duration;
        _announceTimer.Start();
        if (_surface == Surface.Peek) UpdateContent();   // 滑鼠正停在島上:直接換成提示,時間到再換回來
        else ShowSurface(Surface.Announce);
    }

    private void OnAnnounceTimerTick(object? sender, EventArgs e)
    {
        _announceTimer.Stop();
        if (_announced is null) return;
        _announced = null;
        switch (_surface)
        {
            case Surface.Peek:
                UpdateContent();   // 換回預覽的內容
                break;
            case Surface.Announce:
                if (IsCursorOverCapsule()) ShowSurface(Surface.Peek);
                else ToRest();
                break;
        }
    }

    /// <summary>
    /// 依狀態決定一排裡顯示什麼:
    /// Peek / Announce 時,提示中的模組 → 否則第一個有活動的模組 → 都沒有就是「日期 | 時間 | 小提示」;
    /// Idle / Hidden 只顯示時間 + 小提示;左側日期只在 Peek 且沒有活動時出現。
    /// </summary>
    private void UpdateContent(bool instant = false)
    {
        IIslandModule? activity = null;
        if (_surface is Surface.Peek or Surface.Announce)
            activity = _announced is IIslandActivity { IsActivityActive: true } ? _announced : CurrentActivity;
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
        bool showDate = !showActivity && _surface == Surface.Peek;
        if (instant)
        {
            SetOpacity(ActivitySlot, showActivity ? 1 : 0);
            SetOpacity(CenterSlot, showActivity ? 0 : 1);
            SetOpacity(BadgeStrip, showActivity ? 0 : 1);
            SetOpacity(LeftSlot, showDate ? 1 : 0);
        }
        else
        {
            var d = Ms(160);
            Fade(ActivitySlot, showActivity ? 1 : 0, d);
            Fade(CenterSlot, showActivity ? 0 : 1, d);
            Fade(BadgeStrip, showActivity ? 0 : 1, d);
            Fade(LeftSlot, showDate ? 1 : 0, d, showDate ? Ms(60) : null);
        }
        UpdateCircles();
    }

    /// <summary>「閒置時隱藏」切換後:Hidden ↔ Idle。</summary>
    private void ApplyIdleMode()
    {
        if (_surface == Surface.Hidden && IdleVisible) ShowSurface(Surface.Idle);
        else if (_surface == Surface.Idle && !IdleVisible) HideSurface();
    }

    // ── 左右小圓(多個即時狀態) ───────────────────────────

    /// <summary>一個小圓 = 某個模組的第 Index 個小圓(捷徑可以有兩個)。</summary>
    private sealed record CircleRef(IIslandModule Module, int Index);

    private readonly Dictionary<(IIslandModule, int), FrameworkElement?> _compactViews = [];

    /// <summary>
    /// 只在 Peek、沒有提示、而且長條正在顯示某個活動時出現:
    /// 其餘有東西可看的模組依 liveOrder 取最多兩個,一個時放右邊,兩個時左右各一。
    /// </summary>
    private void UpdateCircles()
    {
        var pill = _surface == Surface.Peek && _announced is null ? CurrentActivity : null;
        var picks = new List<CircleRef>();
        // 有活動:其他有東西可看的模組放小圓;沒有活動(日期 / 時間那一排):只放「沒有活動也顯示」的(例如捷徑)
        bool idleRow = pill is null && _surface == Surface.Peek && _announced is null;
        bool focusing = FocusModule is not null;   // 專注中:只顯示倒數,不放小圓
        if (!focusing && (pill is not null || idleRow))
        {
            foreach (var m in LiveOrdered)
            {
                if (picks.Count == 2) break;
                if (ReferenceEquals(m, pill) || m is not IIslandCompact c) continue;
                if (idleRow && !c.ShowWithoutActivity) continue;
                if (_settings.LiveHidden.Contains(m.Id, StringComparer.OrdinalIgnoreCase)) continue;
                int count;
                try { count = Math.Clamp(c.CompactCount, 1, 2); } catch { count = 1; }
                for (int i = 0; i < count && picks.Count < 2; i++)
                {
                    bool relevant;
                    try { relevant = c.IsCompactRelevantAt(i); } catch { relevant = false; }
                    if (!relevant) continue;
                    var at = new CircleRef(m, i);
                    if (CompactView(at) is null) continue;
                    picks.Add(at);
                }
            }
        }

        var (left, right) = picks.Count switch
        {
            0 => ((CircleRef?)null, (CircleRef?)null),
            1 => (null, picks[0]),
            _ => (picks[0], picks[1]),
        };
        // 只要有任何小圓,兩側都保留位置(空的那側看不見),長條才會一直在正中間
        bool reserve = picks.Count > 0;
        ShowCircle(LeftCircle, LeftCircleSlot, left, reserve);
        ShowCircle(RightCircle, RightCircleSlot, right, reserve);
    }

    private FrameworkElement? CompactView(CircleRef at)
    {
        if (!_compactViews.TryGetValue((at.Module, at.Index), out var view))
        {
            view = SafeCreate(at.Module, () => ((IIslandCompact)at.Module).CreateCompactView(at.Index));
            _compactViews[(at.Module, at.Index)] = view;
        }
        return view;
    }

    private void ShowCircle(Border circle, ContentControl slot, CircleRef? module, bool reserve)
    {
        if (ReferenceEquals(circle, _stripCircle) && !Equals(circle.Tag, module)) CloseStrip(instant: true);
        if (module is null)
        {
            var empty = reserve ? Visibility.Hidden : Visibility.Collapsed;
            if (circle.Visibility != Visibility.Visible)
            {
                circle.Visibility = empty;
                return;
            }
            circle.Tag = null;
            var fade = new DoubleAnimation(0, new Duration(Ms(120)));
            fade.Completed += (_, _) =>
            {
                if (circle.Tag is not null) return;
                slot.Content = null;
                bool stillReserve = LeftCircle.Tag is not null || RightCircle.Tag is not null;
                circle.Visibility = stillReserve ? Visibility.Hidden : Visibility.Collapsed;
                if (!stillReserve)
                    foreach (var c in new[] { LeftCircle, RightCircle })
                        if (c.Tag is null && c.Visibility == Visibility.Hidden) c.Visibility = Visibility.Collapsed;
            };
            circle.BeginAnimation(OpacityProperty, fade);
            return;
        }

        bool appearing = circle.Visibility != Visibility.Visible || circle.Tag is null;
        circle.Tag = module;
        var view = CompactView(module);
        if (!ReferenceEquals(slot.Content, view)) slot.Content = view;
        circle.Visibility = Visibility.Visible;
        if (appearing)
            circle.BeginAnimation(OpacityProperty, new DoubleAnimation(1, new Duration(Ms(180)))
            {
                BeginTime = Ms(60),
            });
    }

    private void OnCircleClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not Border { Tag: CircleRef at }) return;
        // 點小圓:使用者設定(或模組預設)為「直接執行」→ 執行模組的動作;否則打開分頁
        if (at.Module is IIslandCompact compact && CircleActions.UseAction(_settings.CircleActions, at.Module.Id, compact))
        {
            try { compact.RunCompactAction(at.Index); }
            catch (Exception ex) { Log.Write($"模組 {at.Module.Id} 小圓動作失敗:{ex.Message}"); }
            return;
        }
        ToExpanded(at.Module, pin: true);
    }

    /// <summary>小瀏海只在完全隱藏(而且設定打開)時出現。</summary>
    private void UpdateNotch() =>
        Notch.Visibility = _settings.ShowNotch && _surface == Surface.Hidden && !_leaving ? Visibility.Visible : Visibility.Collapsed;

    // ── 小圓延伸(例如捷徑:列出開著的視窗) ──────────────
    // 滑鼠停在小圓上 0.2 秒 → 向外(右小圓往右、左小圓往左)拉長;Width 增加多少、外側 Margin 就減多少,
    // 所以整排的佔位不變,長條不會被擠歪。視窗只有在延伸時才加寬,收起後縮回。

    private DispatcherTimer? _stripTimer;
    private Border? _stripPending, _stripCircle;
    private FrameworkElement? _strip;
    private IIslandCompactStrip? _stripOwner;
    private double _stripTarget;

    /// <summary>延伸列最多再長多寬(每一側)。</summary>
    private const double MaxStripExtra = 300;

    /// <summary>
    /// 有可以延伸的小圓時,一排的視窗兩側先預留延伸的空間。
    /// 不在延伸當下才改視窗大小:改大小和重畫不同步,整個島會抽一下。
    /// </summary>
    private double StripReserve
    {
        get
        {
            foreach (var m in _modules)
            {
                if (m is not IIslandCompactStrip s) continue;
                try { if (s.HasStrip(0) || s.HasStrip(1)) return MaxStripExtra; } catch { }
            }
            return 0;
        }
    }

    private void OnCircleMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border { Tag: CircleRef at } circle || ReferenceEquals(circle, _stripCircle)) return;
        if (_surface != Surface.Peek || at.Module is not IIslandCompactStrip owner) return;
        bool has;
        try { has = owner.HasStrip(at.Index); } catch { has = false; }
        if (!has) return;
        _stripPending = circle;
        if (_stripTimer is null)
        {
            _stripTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
            _stripTimer.Tick += (_, _) => OpenStrip();
        }
        _stripTimer.Stop();
        _stripTimer.Start();
    }

    private void OnCircleMouseLeave(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, _stripPending)) { _stripTimer?.Stop(); _stripPending = null; }
        if (ReferenceEquals(sender, _stripCircle) && _openMenus.Count == 0) CloseStrip();
    }

    private (Canvas Host, ContentControl Slot) StripParts(Border circle) =>
        ReferenceEquals(circle, LeftCircle) ? (LeftCircleStrip, LeftCircleSlot) : (RightCircleStrip, RightCircleSlot);

    private void OpenStrip()
    {
        _stripTimer?.Stop();
        var circle = _stripPending;
        _stripPending = null;
        if (circle is not { Tag: CircleRef at } || at.Module is not IIslandCompactStrip owner) return;
        if (_surface != Surface.Peek || !circle.IsMouseOver) return;
        CloseStrip(instant: true);

        bool left = ReferenceEquals(circle, LeftCircle);
        var strip = SafeCreate(at.Module, () => owner.OpenStrip(at.Index, left, _collapsedSize.Height - 2));
        if (strip is null) return;
        if (_settings.StartButton == StartButtonPlace.Strip) strip = WithStartButton(strip, left);
        var (host, slot) = StripParts(circle);
        _stripCircle = circle;
        _strip = strip;
        _stripOwner = owner;
        host.Children.Add(strip);
        if (left) Canvas.SetRight(strip, 0); else Canvas.SetLeft(strip, 0);
        host.Visibility = Visibility.Visible;
        slot.Visibility = Visibility.Hidden;   // 延伸列的第一格就是原本的圖示
        strip.SizeChanged += OnStripSizeChanged;
    }

    private void OnStripSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _strip) || _stripCircle is null) return;
        var strip = (FrameworkElement)sender;
        double width = e.NewSize.Width + strip.Margin.Left + strip.Margin.Right + 2;   // + 邊框
        double target = Math.Clamp(Math.Round(width), _collapsedSize.Height, _collapsedSize.Height + MaxStripExtra);
        if (Math.Abs(target - _stripTarget) < 0.5) return;
        _stripTarget = target;
        AnimateStrip(_stripCircle, target, instant: false);
    }

    /// <summary>
    /// 小圓寬度的動畫:不直接動畫 Width / Margin(版面會各自四捨五入,中間的長條會跟著晃 1px),
    /// 而是動畫一個代理數值,每一格取整數後同時設定 Width 和外側 Margin,整排的佔位永遠不變。
    /// </summary>
    private sealed class StripWidth : Animatable
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(StripWidth),
            new PropertyMetadata(0.0, (d, e) => ((StripWidth)d).Changed?.Invoke((double)e.NewValue)));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Action<double>? Changed;
        protected override Freezable CreateInstanceCore() => new StripWidth();
    }

    private readonly Dictionary<Border, StripWidth> _stripWidths = [];

    private StripWidth StripWidthOf(Border circle)
    {
        if (_stripWidths.TryGetValue(circle, out var w)) return w;
        w = new StripWidth { Value = _collapsedSize.Height };
        bool left = ReferenceEquals(circle, LeftCircle);
        w.Changed = v =>
        {
            double width = Math.Max(_collapsedSize.Height, Math.Round(v));
            double extra = width - _collapsedSize.Height;
            circle.Width = width;
            circle.Margin = left ? new Thickness(-extra, 0, 6, 0) : new Thickness(6, 0, -extra, 0);
            HotZone.Width = HotZoneBaseWidth + 2 * extra;   // 延伸列上方也算在島上
        };
        _stripWidths[circle] = w;
        return w;
    }

    private void AnimateStrip(Border circle, double width, bool instant)
    {
        var proxy = StripWidthOf(circle);
        if (instant)
        {
            proxy.BeginAnimation(StripWidth.ValueProperty, null);
            proxy.Value = width;
            proxy.Changed?.Invoke(width);   // 值沒變時也要把 Width / Margin 設回來
            return;
        }
        double from = proxy.Value;
        bool growing = width > from;
        var duration = new Duration(Ms(growing ? 260 : 180));
        IEasingFunction ease = growing ? new BackEase { Amplitude = 0.2, EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseOut };
        var anim = new DoubleAnimation(width, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        anim.Completed += (_, _) => proxy.Value = width;
        proxy.Value = from;
        proxy.BeginAnimation(StripWidth.ValueProperty, anim);
        proxy.Value = width;   // 動畫結束後的基底值
    }

    /// <summary>收起延伸列,小圓縮回原本大小;縮完再把視窗縮回。</summary>
    private void CloseStrip(bool instant = false)
    {
        _stripTimer?.Stop();
        _stripPending = null;
        if (_stripCircle is not { } circle) return;
        try { _stripOwner?.CloseStrip(); } catch (Exception ex) { Log.Write($"小圓延伸收起失敗:{ex.Message}"); }
        if (_strip is not null) _strip.SizeChanged -= OnStripSizeChanged;
        var (host, slot) = StripParts(circle);
        host.Children.Clear();
        host.Visibility = Visibility.Collapsed;
        slot.Visibility = Visibility.Visible;
        _stripCircle = null;
        _strip = null;
        _stripOwner = null;
        _stripTarget = 0;

        double size = _collapsedSize.Height;
        AnimateStrip(circle, size, instant);
    }

    // ── 拖放:交給第一個願意接收的模組 ───────────────────

    private (IIslandModule Module, IIslandDropTarget Target)? FindDropTarget(IDataObject data)
    {
        // 展開中:優先交給目前開著的分頁(例如在「捷徑」分頁拖程式進來 → 加成捷徑)
        if (IsExpanded && _active is IIslandDropTarget activeTarget)
        {
            try
            {
                if (activeTarget.CanAccept(data)) return (_active, activeTarget);
            }
            catch (Exception ex)
            {
                Log.Write($"模組 {_active.Id} CanAccept 失敗:{ex.Message}");
            }
        }
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
        ToExpanded(found.Value.Module, pin: false);
        Capsule.BorderBrush = DragHighlightBrush;
    }

    private void OnCapsuleDragOver(object sender, DragEventArgs e)
    {
        var found = FindDropTarget(e.Data);
        e.Effects = found is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        if (found is null) return;

        _dragInside = true;
        _outsideTicks = 0;
        if (!IsExpanded || !ReferenceEquals(_active, found.Value.Module)) ToExpanded(found.Value.Module, pin: false);
        Capsule.BorderBrush = DragHighlightBrush;
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
        ApplyCapsuleChrome();
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
            ToExpanded(found.Value.Module, pin: false);
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
        if (m is not null) ToExpanded(m, pin: false);
    }

    void IShellController.Collapse() => CollapseExpanded();

    bool IShellController.SendToModule(string moduleId, IDataObject data)
    {
        var m = _modules.FirstOrDefault(x => string.Equals(x.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (m is not IIslandDropTarget target) return false;
        try
        {
            if (!target.CanAccept(data)) return false;
            target.Accept(data);
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"交給模組 {moduleId} 失敗:{ex.Message}");
            return false;
        }
    }

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

    // ── 設定視窗 ─────────────────────────────────────────

    private Settings.SettingsWindow? _settingsWindow;

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>打開設定視窗(已經開著就叫到前面);page = 要直接切到的頁面,例如第一次使用時的「歡迎」。</summary>
    internal void OpenSettings(string? page = null)
    {
        CloseMenus();
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            if (page is not null) _settingsWindow.Navigate(page);
            _settingsWindow.Activate();
            return;
        }
        Log.Write($"打開設定視窗{(page is null ? "" : $"({page})")}");
        _settingsWindow = new Settings.SettingsWindow(_settings, this, _modules, page);
        _settingsWindow.Closed += (_, _) => { _settingsWindow = null; Log.Write("設定視窗關閉"); };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // ── 主題 ─────────────────────────────────────────────

    private FontFamily? _defaultFont;
    private DispatcherTimer? _previewTimer;

    /// <summary>套用外觀主題:強調色 / 底色(AccentTheme)、圓角、邊框、字型、動畫速度。</summary>
    internal void ApplyTheme(bool colors = true)
    {
        var t = _settings.Theme;
        s_animScale = t.SpeedFactor;
        if (colors) AccentTheme.Configure(t);

        var border = new Thickness(t.ShowBorder ? 1 : 0);
        Capsule.BorderThickness = border;
        LeftCircle.BorderThickness = border;
        RightCircle.BorderThickness = border;

        _defaultFont ??= FontFamily;
        FontFamily = string.IsNullOrWhiteSpace(t.FontFamily) ? _defaultFont : new FontFamily(t.FontFamily);

        Capsule.CornerRadius = new CornerRadius(IsExpanded ? ExpandedRadius : CollapsedRadius);
        foreach (var circle in new[] { LeftCircle, RightCircle }) circle.CornerRadius = new CornerRadius(CollapsedRadius);
    }

    /// <summary>設定頁「在島上預覽」:讓島出現 3 秒(滑鼠不在島上就自己收回)。</summary>
    internal void PreviewTheme()
    {
        if (IsExpanded || !IsVisible) return;
        ShowSurface(Surface.Peek);
        _previewTimer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        _previewTimer.Tick -= OnPreviewTimerTick;
        _previewTimer.Tick += OnPreviewTimerTick;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        _previewTimer!.Stop();
        if (_surface == Surface.Peek && !IsCursorOverCapsule()) ToRest();
    }

    /// <summary>展開畫面右下角的齒輪:打開設定,島收起來。</summary>
    // ── 開始按鈕 ─────────────────────────────────────────

    private void ApplyStartButton()
    {
        PeekStartButton.Visibility = _settings.StartButton == StartButtonPlace.Row ? Visibility.Visible : Visibility.Collapsed;
        TabStartButton.Visibility = _settings.StartButton == StartButtonPlace.Tabs ? Visibility.Visible : Visibility.Collapsed;
        if (_expandedViewsBuilt) Dispatcher.BeginInvoke(UpdateTabRow, DispatcherPriority.Loaded);
    }

    /// <summary>延伸列最後加一個分隔線 + 開始按鈕(往左長時放最前面)。</summary>
    private FrameworkElement WithStartButton(FrameworkElement strip, bool towardLeft)
    {
        var button = new Button
        {
            Style = (Style)FindResource("IconButton"),
            ContentTemplate = (DataTemplate)FindResource("StartGlyph"),
            Content = "",
            ToolTip = "開始",
            Margin = towardLeft ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 8, 0),
        };
        button.Click += OnStartClick;
        var sep = new Border { Width = 1, Height = (_collapsedSize.Height - 2) * 0.5, Margin = new Thickness(2, 0, 6, 0), Background = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), VerticalAlignment = VerticalAlignment.Center };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Height = _collapsedSize.Height - 2 };
        if (towardLeft)
        {
            sep.Margin = new Thickness(6, 0, 2, 0);
            panel.Children.Add(button);
            panel.Children.Add(sep);
            panel.Children.Add(strip);
        }
        else
        {
            panel.Children.Add(strip);
            panel.Children.Add(sep);
            panel.Children.Add(button);
        }
        return panel;
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // 一排左邊的按鈕跟著日期淡出(例如正在播放時看不見):當成點膠囊
        if (ReferenceEquals(sender, PeekStartButton) && LeftSlot.Opacity < 0.5)
        {
            ToExpanded(CurrentActivity, pin: true);
            return;
        }
        try { Taskbar.OpenStart(); }
        catch (Exception ex) { Log.Write($"打開開始功能表失敗:{ex.Message}"); }
        CloseStrip();
        if (IsExpanded) CollapseExpanded(force: true);
    }

    // ── 分頁那一排 ───────────────────────────────────────

    private void OnTabWheel(object sender, MouseWheelEventArgs e)
    {
        if (TabScroller.ExtentWidth <= TabScroller.ViewportWidth + 1) return;
        TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset - e.Delta * 0.5);
        e.Handled = true;
    }

    private void OnTabScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateTabRow();

    private void OnTabRowSizeChanged(object sender, SizeChangedEventArgs e) => UpdateTabRow();

    /// <summary>
    /// 分頁放得下:左邊留和右邊圖示一樣寬的空白,分頁在整個面板置中;
    /// 放不下:不留白、可以左右捲,被切到的那一邊淡出提示還有東西。
    /// </summary>
    private void UpdateTabRow()
    {
        double tools = TabTools.ActualWidth + TabTools.Margin.Left;
        double tabs = TabScroller.ExtentWidth;   // 分頁本身的寬度(捲動範圍)
        double balance = tabs <= TabRow.ActualWidth - 2 * tools ? tools : 0;
        if (Math.Abs(TabBalance.Width.Value - balance) > 0.5) TabBalance.Width = new GridLength(balance);

        double hidden = TabScroller.ExtentWidth - TabScroller.ViewportWidth;
        if (hidden <= 1)
        {
            TabScroller.OpacityMask = null;
            return;
        }
        byte left = TabScroller.HorizontalOffset > 1 ? (byte)0 : (byte)255;
        byte right = TabScroller.HorizontalOffset < hidden - 1 ? (byte)0 : (byte)255;
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(left, 0, 0, 0), 0));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.1));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.9));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(right, 0, 0, 0), 1));
        mask.Freeze();
        TabScroller.OpacityMask = mask;
    }

    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OnSettingsClick(sender, e);
        CollapseExpanded(force: true);
    }

    /// <summary>設定視窗改了可以立即套用的項目(閒置隱藏、全螢幕隱藏、螢幕)後呼叫。</summary>
    internal void ApplySettingsLive()
    {
        _settings.Normalize();
        ApplyLayout();
        Capsule.CornerRadius = new CornerRadius(IsExpanded ? ExpandedRadius : _collapsedSize.Height / 2);
        if (_surface != Surface.Hidden || _leaving)
            SetSize(_surface switch
            {
                Surface.Expanded => _expandedSize,
                Surface.Peek or Surface.Announce => _peekSize,
                _ => _collapsedSize,
            });
        UseBigWindow(_bigWindow, force: true);
        UpdateNotch();
        ApplyStartButton();
        ApplyIdleMode();
        if (_settings.HideInFullscreen) _fullscreen?.Evaluate();
        else if (!IsVisible) OnFullscreenChanged(false);
        Reposition("設定");
    }
}

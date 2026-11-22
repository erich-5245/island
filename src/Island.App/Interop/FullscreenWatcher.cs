using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Island.App.Infrastructure;
using static Island.App.Interop.NativeMethods;

namespace Island.App.Interop;

/// <summary>
/// 偵測「島所在的螢幕」上是否有全螢幕程式(影片、遊戲、簡報、瀏覽器 F11)。
/// 不輪詢,只在這些時機重新判斷一次:
///   1. 系統的 ABN_FULLSCREENAPP 通知(註冊成不佔空間的 AppBar 才收得到),
///      涵蓋「同一個視窗切換成全螢幕」,例如 YouTube 按 F;
///   2. 前景視窗切換(由 TopmostKeeper 轉告);
///   3. 螢幕變更。
/// 判斷方式:依 Z 順序找出該螢幕上最上面的一般應用程式視窗,看它是否蓋滿整個螢幕。
/// </summary>
internal sealed class FullscreenWatcher : IDisposable
{
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(250);

    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW",                                   // 桌面
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",              // 工作列
        "Windows.UI.Core.CoreWindow",                           // 開始功能表、搜尋、通知中心
        "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland",
        "ForegroundStaging", "MultitaskingViewFrame", "TaskSwitcherWnd",
    };

    private readonly IntPtr _hwnd;
    private readonly Func<IntPtr> _monitor;
    private readonly Action<bool> _changed;
    private readonly uint _callbackMessage;
    private readonly DispatcherTimer _settle;
    private bool _registered;

    public bool IsFullscreen { get; private set; }

    public FullscreenWatcher(IntPtr hwnd, Dispatcher dispatcher, Func<IntPtr> monitor, Action<bool> changed)
    {
        _hwnd = hwnd;
        _monitor = monitor;
        _changed = changed;

        _settle = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = SettleDelay };
        _settle.Tick += (_, _) => { _settle.Stop(); Evaluate(); };

        _callbackMessage = RegisterWindowMessage("Island.AppBarNotify");
        var abd = NewAppBarData();
        _registered = SHAppBarMessage(ABM_NEW, ref abd) != UIntPtr.Zero;
        if (!_registered) Log.Write("AppBar 註冊失敗,全螢幕偵測只依前景視窗切換");
    }

    /// <summary>由視窗的 WndProc 轉呼叫;是 AppBar 通知時回傳 true。</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (_callbackMessage == 0 || msg != (int)_callbackMessage) return false;
        if (wParam.ToInt64() == ABN_FULLSCREENAPP) Schedule();
        return true;
    }

    /// <summary>稍等視窗大小穩定後再判斷(切換全螢幕時視窗會先換前景、再改大小)。</summary>
    public void Schedule()
    {
        _settle.Stop();
        _settle.Start();
    }

    public void Evaluate()
    {
        bool now;
        try { now = Detect(); }
        catch (Exception ex)
        {
            Log.Write($"全螢幕偵測失敗:{ex.Message}");
            now = false;
        }

        if (now == IsFullscreen) return;
        IsFullscreen = now;
        _changed(now);
    }

    private bool Detect()
    {
        // 獨佔全螢幕的 D3D 遊戲、PowerPoint 簡報模式:系統直接告訴我們
        if (SHQueryUserNotificationState(out int state) == 0 && (state == 3 || state == 4))
            return true;

        var monitorHandle = _monitor();
        var monitor = MonitorInfo.GetAll().FirstOrDefault(m => m.Handle == monitorHandle);
        if (monitor is null) return false;
        var bounds = monitor.Bounds;

        IntPtr top = IntPtr.Zero;
        RECT topRect = default;
        var cls = new StringBuilder(64);

        // EnumWindows 依 Z 順序由上到下列出頂層視窗
        EnumWindows((h, _) =>
        {
            if (h == _hwnd || !IsCandidate(h, cls)) return true;
            if (!GetWindowRect(h, out var r)) return true;
            if (!Intersects(r, bounds)) return true;
            top = h;
            topRect = r;
            return false;   // 找到這個螢幕上最上面的應用程式視窗
        }, IntPtr.Zero);

        if (top == IntPtr.Zero) return false;
        // 一般最大化的視窗(有標題列;工作列自動隱藏時)也會蓋滿螢幕,但不是全螢幕。
        // 無邊框最大化(很多播放器、遊戲的全螢幕做法)沒有標題列,仍算全螢幕。
        long style = GetWindowLongPtr(top, GWL_STYLE).ToInt64();
        if (IsZoomed(top) && (style & WS_CAPTION) == WS_CAPTION) return false;
        return topRect.Left <= bounds.Left && topRect.Top <= bounds.Top
               && topRect.Right >= bounds.Right && topRect.Bottom >= bounds.Bottom;
    }

    private static bool IsCandidate(IntPtr h, StringBuilder cls)
    {
        long style = GetWindowLongPtr(h, GWL_STYLE).ToInt64();
        if ((style & WS_VISIBLE) == 0 || (style & WS_MINIMIZE) != 0) return false;

        long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        if ((ex & WS_EX_TOOLWINDOW) != 0) return false;          // 浮動小工具、其他動態島、提示框

        // 被 DWM 隱藏的視窗(背景中的 UWP 等)看不到,不算
        if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        cls.Clear();
        GetClassName(h, cls, cls.Capacity);
        return !IgnoredClasses.Contains(cls.ToString());
    }

    private static bool Intersects(RECT a, RECT b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private APPBARDATA NewAppBarData() => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
        uCallbackMessage = _callbackMessage,
    };

    public void Dispose()
    {
        _settle.Stop();
        if (!_registered) return;
        var abd = NewAppBarData();
        SHAppBarMessage(ABM_REMOVE, ref abd);
        _registered = false;
    }
}

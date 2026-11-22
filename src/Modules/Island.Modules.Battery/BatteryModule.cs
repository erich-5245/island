using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Island.Core;
using Microsoft.Win32;

namespace Island.Modules.Battery;

/// <summary>
/// 充電 / 電量提示(只在有電池的電腦上啟用)。
/// - 插上電源:膠囊出現「⚡ 充電中 78%」;拔掉:「使用電池 78%」。
/// - 沒插電、電量降到 20% 與 10% 時各提醒一次(重新充電後重置)。
/// - 充電中(未滿)或電量偏低時,滑鼠移過去會佔一個小圓:電量環。
/// 事件驅動:系統的電源狀態變更通知(SystemEvents.PowerModeChanged → StatusChange),不輪詢。
/// </summary>
public sealed class BatteryModule : IslandModuleBase, IIslandActivity, IIslandCompact
{
    private static readonly TimeSpan AnnounceFor = TimeSpan.FromMilliseconds(2500);
    private static readonly int[] LowLevels = [20, 10];

    private DispatcherTimer? _activeTimer;
    private BatteryView? _view;
    private BatteryRingView? _ring;
    private bool _active;
    private bool _hasBattery;
    private readonly HashSet<int> _lowAlerted = [];

    public override string Id => "battery";
    public override string Title => "電量";

    public int Percent { get; private set; } = -1;
    public bool OnAc { get; private set; }
    public bool Charging { get; private set; }
    /// <summary>提示時顯示的文字(例如「充電中」「使用電池」「電量低」)。</summary>
    public string Message { get; private set; } = "";

    public bool IsLow => !OnAc && Percent >= 0 && Percent <= LowLevels[0];

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _active;
    public bool IsCompactRelevant => _hasBattery && ((OnAc && Percent < 100) || IsLow);

    protected override void OnInitialize()
    {
        var s = Read();
        _hasBattery = s is not null;
        if (!_hasBattery)
        {
            Host.Log("電量:這台電腦沒有電池,停用");
            return;
        }
        (Percent, OnAc, Charging) = s!.Value;
        Host.Log($"電量:{Percent}% {(OnAc ? "插電" : "電池")}");

        _activeTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = AnnounceFor + TimeSpan.FromMilliseconds(300) };
        _activeTimer.Tick += (_, _) => { _activeTimer.Stop(); SetActive(false); };
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public FrameworkElement CreateActivityView() => _view ??= new BatteryView(this);
    public FrameworkElement CreateCompactView() => _ring ??= new BatteryRingView(this);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.StatusChange) return;
        Host.Dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        if (Read() is not { } s) return;
        var (percent, onAc, charging) = s;
        bool plugChanged = onAc != OnAc;
        bool changed = plugChanged || percent != Percent || charging != Charging;
        Percent = percent;
        OnAc = onAc;
        Charging = charging;
        if (!changed) return;

        _view?.Update();
        _ring?.Update();

        if (onAc) _lowAlerted.Clear();

        if (plugChanged)
        {
            Show(onAc ? (percent >= 100 ? "已充飽" : "充電中") : "使用電池");
        }
        else if (onAc && percent >= 100 && changed && Message != "已充飽")
        {
            Show("已充飽");
        }
        else if (!onAc)
        {
            foreach (var level in LowLevels)
            {
                if (percent > level || !_lowAlerted.Add(level)) continue;
                Show(level <= 10 ? "電量很低,請接上電源" : "電量低");
                break;
            }
        }

        // 小圓的相關度只會在上面這些提示時改變;提示開始/結束都會觸發 ActivityChanged,外殼會重新排列
    }

    private void Show(string message)
    {
        Message = message;
        _view?.Update();
        Host.Log($"電量:{message} {Percent}%");
        SetActive(true);
        Host.Announce(Id, AnnounceFor);
        _activeTimer!.Stop();
        _activeTimer.Start();
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Win32 ─────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    /// <summary>讀電源狀態;沒有電池(或讀不到)回傳 null。</summary>
    private static (int Percent, bool OnAc, bool Charging)? Read()
    {
        if (!GetSystemPowerStatus(out var s)) return null;
        if ((s.BatteryFlag & 128) != 0 || s.BatteryFlag == 255 || s.BatteryLifePercent > 100) return null;   // 沒有電池 / 未知
        return (s.BatteryLifePercent, s.ACLineStatus == 1, (s.BatteryFlag & 8) != 0);
    }

    public override void Dispose()
    {
        _activeTimer?.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        base.Dispose();
    }
}

using System.Windows.Threading;
using Microsoft.Win32;

namespace Island.App.Interop;

/// <summary>
/// 監聽螢幕插拔、解析度/縮放變更、睡眠喚醒,去抖動後通知重新定位。
/// 拔插螢幕時 Windows 會連續送出好幾個事件,而且螢幕資訊可能晚一點才穩定,
/// 所以:最後一個事件後 400ms 定位一次,再過 1.5 秒確認一次。全部是事件驅動,沒有輪詢。
/// </summary>
internal sealed class DisplayWatcher : IDisposable
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(1500);

    private readonly Dispatcher _dispatcher;
    private readonly Action _onChanged;
    private readonly DispatcherTimer _timer;
    private bool _settlePending;

    public DisplayWatcher(Dispatcher dispatcher, Action onChanged)
    {
        _dispatcher = dispatcher;
        _onChanged = onChanged;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _timer.Tick += OnTick;

        SystemEvents.DisplaySettingsChanged += OnSystemEvent;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <summary>要求重新定位(可從任何執行緒呼叫,會合併連續的要求)。</summary>
    public void Schedule()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(Schedule);
            return;
        }
        _timer.Stop();
        _timer.Interval = FirstDelay;
        _settlePending = true;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _onChanged();
        if (_settlePending)
        {
            _settlePending = false;
            _timer.Interval = SettleDelay;
            _timer.Start();
        }
    }

    private void OnSystemEvent(object? sender, EventArgs e) => Schedule();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Schedule();
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnSystemEvent;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}

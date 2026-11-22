using System.Windows;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.Osd;

public enum OsdKind { Volume, Brightness }

/// <summary>
/// 音量 / 亮度提示:按音量鍵、調亮度時,膠囊從上緣出現一條「圖示 + 長條 + 數字」約 1.5 秒。
/// 連續按會延長顯示、長條即時跟著動。沒有展開畫面(不佔分頁)。
/// 完全事件驅動,平常不耗資源。
/// </summary>
public sealed class OsdModule : IslandModuleBase, IIslandActivity
{
    private static readonly TimeSpan ShowFor = TimeSpan.FromMilliseconds(1500);

    private readonly AudioWatcher _audio = new();
    private readonly BrightnessWatcher _brightness = new();
    private DispatcherTimer? _hideTimer;
    private OsdView? _view;
    private bool _active;

    private float _lastVolume = -1;
    private bool _lastMuted;
    private int _lastBrightness = -1;

    public override string Id => "osd";
    public override string Title => "提示";

    public OsdKind Kind { get; private set; } = OsdKind.Volume;
    /// <summary>0~1。</summary>
    public double Level { get; private set; }
    public bool Muted { get; private set; }

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _active;

    protected override void OnInitialize()
    {
        // 比 Announce 的顯示時間多一點,確保提示結束前活動一直有效
        _hideTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = ShowFor + TimeSpan.FromMilliseconds(300) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); SetActive(false); };

        _audio.Changed += (level, muted) => Host.Dispatcher.BeginInvoke(() => OnVolume(level, muted));
        _brightness.Changed += value => Host.Dispatcher.BeginInvoke(() => OnBrightness(value));

        bool audio = _audio.Start();
        bool brightness = _brightness.Start();
        Host.Log($"提示:音量監看 {(audio ? "啟用" : "無法使用")},亮度監看 {(brightness ? "啟用" : "無法使用")}");
    }

    public FrameworkElement CreateActivityView() => _view ??= new OsdView(this);

    private void OnVolume(float level, bool muted)
    {
        // 聲道平衡等變更也會通知,音量和靜音都沒變就不顯示
        if (Math.Abs(level - _lastVolume) < 0.001f && muted == _lastMuted) return;
        _lastVolume = level;
        _lastMuted = muted;
        Show(OsdKind.Volume, level, muted);
    }

    private void OnBrightness(int value)
    {
        if (value == _lastBrightness) return;
        _lastBrightness = value;
        Show(OsdKind.Brightness, value / 100.0, false);
    }

    private void Show(OsdKind kind, double level, bool muted)
    {
        Kind = kind;
        Level = Math.Clamp(level, 0, 1);
        Muted = muted;
        _view?.Update();

        SetActive(true);
        Host.Announce(Id, ShowFor);
        _hideTimer!.Stop();
        _hideTimer.Start();
    }

    private void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void Dispose()
    {
        _hideTimer?.Stop();
        _audio.Dispose();
        _brightness.Dispose();
        base.Dispose();
    }
}

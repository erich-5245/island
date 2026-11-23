using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using Island.Core;
using Microsoft.Win32;

namespace Island.Modules.Timer;

public enum TimerMode { Countdown, Pomodoro }
public enum TimerState { Idle, Running, Paused, Done }
public enum PomodoroPhase { Focus, ShortBreak, LongBreak }

/// <summary>
/// 計時器 / 番茄鐘。
/// - 計時器:選時間 → 開始 → 時間到時膠囊跳出「時間到」並發出提示音。
/// - 番茄鐘:專注 25 分 → 自動開始休息 5 分(每 4 輪休息 15 分)→ 休息結束提醒,按開始進入下一輪。
///
/// 效能:只在「執行中」才有每秒一次的計時器;以「結束時刻」計算剩餘時間,
/// 電腦睡眠或計時器延遲都不會累積誤差。沒有在計時時完全不耗資源。
/// </summary>
public sealed class TimerModule : IslandModuleBase, IIslandActivity, IIslandExpandAware, IIslandCompact
{
    public static readonly TimeSpan FocusLength = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan ShortBreakLength = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LongBreakLength = TimeSpan.FromMinutes(15);
    public const int RoundsPerLongBreak = 4;

    /// <summary>時間到之後,滑鼠移過去仍顯示「時間到」多久。</summary>
    private static readonly TimeSpan DoneLinger = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AnnounceDuration = TimeSpan.FromSeconds(6);

    private DispatcherTimer? _tick;
    private DispatcherTimer? _doneTimer;
    private DateTime _endsAt;
    private TimerView? _view;
    private TimerActivityView? _activityView;
    private TimerRingView? _ring;

    public override string Id => "timer";
    public override string Title => "計時";

    public TimerMode Mode { get; private set; } = TimerMode.Countdown;
    public TimerState State { get; private set; } = TimerState.Idle;
    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    /// <summary>番茄鐘已完成的專注輪數。</summary>
    public int CompletedRounds { get; private set; }

    /// <summary>計時器模式選定的長度。</summary>
    public TimeSpan CountdownLength { get; private set; } = TimeSpan.FromMinutes(5);
    /// <summary>這一段的總長度(用來畫進度)。</summary>
    public TimeSpan Total { get; private set; } = TimeSpan.FromMinutes(5);
    /// <summary>暫停 / 尚未開始時的剩餘時間。</summary>
    private TimeSpan _remainingWhenStopped = TimeSpan.FromMinutes(5);

    public TimeSpan Remaining => State == TimerState.Running
        ? Max(_endsAt - DateTime.Now, TimeSpan.Zero)
        : _remainingWhenStopped;

    /// <summary>剛結束的是哪一段(顯示「時間到」的文字用)。</summary>
    public string DoneMessage { get; private set; } = "";

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => State is TimerState.Running or TimerState.Paused or TimerState.Done;

    protected override void OnInitialize()
    {
        _tick = new DispatcherTimer(DispatcherPriority.Normal, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => OnTick();
        _doneTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = DoneLinger };
        _doneTimer.Tick += (_, _) => { _doneTimer.Stop(); if (State == TimerState.Done) SetState(TimerState.Idle); };
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public FrameworkElement CreateActivityView() => _activityView ??= new TimerActivityView(this);

    public bool IsCompactRelevant => IsActivityActive;
    public FrameworkElement CreateCompactView() => _ring ??= new TimerRingView(this);
    public override FrameworkElement CreateExpandedView() => _view ??= new TimerView(this);

    // ── 操作(由畫面呼叫) ─────────────────────────────────

    public void SetMode(TimerMode mode)
    {
        if (Mode == mode) return;
        Stop();
        Mode = mode;
        Phase = PomodoroPhase.Focus;
        CompletedRounds = 0;
        ResetLength();
        SetState(TimerState.Idle);
    }

    /// <summary>計時器模式:選定長度(只在沒在計時時有效)。</summary>
    public void SetCountdownLength(TimeSpan length)
    {
        if (Mode != TimerMode.Countdown || State is TimerState.Running or TimerState.Paused) return;
        CountdownLength = Clamp(length);
        ResetLength();
        SetState(TimerState.Idle);
    }

    /// <summary>計時器模式:加減分鐘(滾輪)。執行中也可以加減。</summary>
    public void AdjustMinutes(int minutes)
    {
        if (Mode != TimerMode.Countdown) return;
        var delta = TimeSpan.FromMinutes(minutes);
        switch (State)
        {
            case TimerState.Running:
                var newEnd = _endsAt + delta;
                if (newEnd - DateTime.Now < TimeSpan.FromSeconds(1)) return;
                _endsAt = newEnd;
                Total = Clamp(Total + delta);
                break;
            case TimerState.Paused:
                if (_remainingWhenStopped + delta < TimeSpan.FromSeconds(1)) return;
                _remainingWhenStopped += delta;
                Total = Clamp(Total + delta);
                break;
            default:
                CountdownLength = Clamp(CountdownLength + delta);
                ResetLength();
                SetState(TimerState.Idle);
                return;
        }
        Refresh();
    }

    public void StartPause()
    {
        switch (State)
        {
            case TimerState.Running:
                _remainingWhenStopped = Remaining;
                _tick!.Stop();
                SetState(TimerState.Paused);
                break;
            case TimerState.Paused:
                Run(_remainingWhenStopped);
                break;
            default:   // Idle / Done
                if (State == TimerState.Done) ResetLength();
                Run(_remainingWhenStopped);
                break;
        }
    }

    /// <summary>重設:回到這一段的開頭(番茄鐘回到專注第一輪)。</summary>
    public void Reset()
    {
        Stop();
        if (Mode == TimerMode.Pomodoro)
        {
            Phase = PomodoroPhase.Focus;
            CompletedRounds = 0;
        }
        ResetLength();
        SetState(TimerState.Idle);
    }

    /// <summary>番茄鐘:跳過這一段,直接進入下一段。</summary>
    public void Skip()
    {
        if (Mode != TimerMode.Pomodoro) return;
        Stop();
        if (Phase == PomodoroPhase.Focus) CompletedRounds++;
        AdvancePhase();
        ResetLength();
        SetState(TimerState.Idle);
    }

    // ── 內部 ─────────────────────────────────────────────

    private void Run(TimeSpan remaining)
    {
        _doneTimer!.Stop();
        _endsAt = DateTime.Now + remaining;
        _tick!.Start();
        SetState(TimerState.Running);
    }

    private void Stop()
    {
        _tick?.Stop();
        _doneTimer?.Stop();
    }

    private void OnTick()
    {
        if (Remaining <= TimeSpan.Zero) Finish();
        else Refresh(tickOnly: true);
    }

    private void Finish()
    {
        _tick!.Stop();
        _remainingWhenStopped = TimeSpan.Zero;

        if (Mode == TimerMode.Countdown)
        {
            DoneMessage = "時間到";
            Done();
            return;
        }

        if (Phase == PomodoroPhase.Focus)
        {
            // 專注結束 → 自動開始休息
            CompletedRounds++;
            AdvancePhase();
            DoneMessage = Phase == PomodoroPhase.LongBreak ? "專注結束,休息 15 分鐘" : "專注結束,休息 5 分鐘";
            ResetLength();
            PlaySound();
            Run(_remainingWhenStopped);
            Host.Announce(Id, AnnounceDuration);
        }
        else
        {
            // 休息結束 → 等使用者按開始
            AdvancePhase();
            DoneMessage = "休息結束,開始下一輪專注";
            ResetLength();
            Done();
        }
    }

    private void Done()
    {
        SetState(TimerState.Done);
        PlaySound();
        Host.Announce(Id, AnnounceDuration);
        _doneTimer!.Start();
    }

    private void AdvancePhase() => Phase = NextPhase(Phase, CompletedRounds);

    /// <summary>番茄鐘的下一段:專注 → 休息(每 4 輪一次長休息)→ 專注。completedRounds 是已完成的專注輪數。</summary>
    internal static PomodoroPhase NextPhase(PomodoroPhase phase, int completedRounds) =>
        phase == PomodoroPhase.Focus
            ? (completedRounds > 0 && completedRounds % RoundsPerLongBreak == 0 ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak)
            : PomodoroPhase.Focus;

    internal static TimeSpan LengthOf(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => FocusLength,
        PomodoroPhase.ShortBreak => ShortBreakLength,
        _ => LongBreakLength,
    };

    private void ResetLength()
    {
        Total = Mode == TimerMode.Countdown ? CountdownLength : LengthOf(Phase);
        _remainingWhenStopped = Total;
    }

    private void SetState(TimerState state)
    {
        bool wasActive = IsActivityActive;
        State = state;
        Refresh();
        if (wasActive != IsActivityActive) ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 更新畫面。每秒的更新只更新「看得到」的畫面(膠囊隱藏、面板收起時不更新,不耗 CPU);
    /// 畫面重新出現時(IsVisibleChanged)會自己補一次。
    /// </summary>
    private void Refresh(bool tickOnly = false)
    {
        if (_view is not null && (!tickOnly || _view.IsVisible)) _view.Update();
        if (_activityView is not null && (!tickOnly || _activityView.IsVisible)) _activityView.Update();
        if (_ring is not null && (!tickOnly || _ring.IsVisible)) _ring.Update();
    }

    private static void PlaySound()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Media", "Windows Notify Calendar.wav");
            if (File.Exists(path)) new SoundPlayer(path).Play();   // 非同步播放
            else SystemSounds.Asterisk.Play();
        }
        catch { /* 沒有音效裝置時忽略 */ }
    }

    /// <summary>從睡眠喚醒:剩餘時間依結束時刻重算,已經過了就立即結束。</summary>
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Host.Dispatcher.BeginInvoke(() => { if (State == TimerState.Running) OnTick(); });
    }

    public void OnExpanded() => Refresh();
    public void OnCollapsed() { }

    private static TimeSpan Clamp(TimeSpan t) =>
        t < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : t > TimeSpan.FromMinutes(180) ? TimeSpan.FromMinutes(180) : t;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public static string Format(TimeSpan t)
    {
        // 進位到整秒(顯示 0:00 時才真的到)
        var secs = (int)Math.Ceiling(t.TotalSeconds - 0.001);
        if (secs < 0) secs = 0;
        return secs >= 3600 ? $"{secs / 3600}:{secs / 60 % 60:00}:{secs % 60:00}" : $"{secs / 60}:{secs % 60:00}";
    }

    public string StatusText => State switch
    {
        TimerState.Done => DoneMessage,
        TimerState.Paused => "已暫停",
        _ when Mode == TimerMode.Pomodoro => Phase switch
        {
            PomodoroPhase.Focus => $"專注 · 第 {CompletedRounds + 1} 輪",
            PomodoroPhase.ShortBreak => "短休息",
            _ => "長休息",
        },
        TimerState.Running => "計時中",
        _ => "",
    };

    public override void Dispose()
    {
        Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        base.Dispose();
    }
}

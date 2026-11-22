using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Timer;

public partial class TimerView : UserControl
{
    private readonly TimerModule _module;
    private bool _updating;   // 程式自己勾選 RadioButton 時,不要觸發操作

    internal TimerView(TimerModule module)
    {
        _module = module;
        InitializeComponent();
        ((FrameworkElement)ProgressFill.Parent).SizeChanged += (_, _) => UpdateProgress();
        IsVisibleChanged += (_, _) => { if (IsVisible) Update(); };
        Update();
    }

    public void Update()
    {
        _updating = true;
        try
        {
            var m = _module;
            bool countdown = m.Mode == TimerMode.Countdown;
            bool active = m.State is TimerState.Running or TimerState.Paused;

            CountdownTab.IsChecked = countdown;
            PomodoroTab.IsChecked = !countdown;
            StatusText.Text = m.StatusText;

            TimeText.Text = TimerModule.Format(m.Remaining);
            StartButton.Content = m.State == TimerState.Running ? "\uE769" : "\uE768";   // 暫停 / 開始
            StartButton.ToolTip = m.State == TimerState.Running ? "暫停" : "開始";
            SkipButton.Visibility = countdown ? Visibility.Collapsed : Visibility.Visible;
            ResetButton.Visibility = active || m.State == TimerState.Done || (!countdown && m.CompletedRounds > 0)
                ? Visibility.Visible : Visibility.Collapsed;

            Presets.Visibility = countdown ? Visibility.Visible : Visibility.Collapsed;
            Rounds.Visibility = countdown ? Visibility.Collapsed : Visibility.Visible;
            if (countdown)
            {
                int minutes = (int)Math.Round(m.CountdownLength.TotalMinutes);
                foreach (RadioButton chip in Presets.Children)
                {
                    chip.IsEnabled = !active;
                    chip.IsChecked = int.Parse((string)chip.Tag) == minutes;
                }
            }
            else
            {
                UpdateRounds();
            }
            UpdateProgress();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateProgress()
    {
        double track = ((FrameworkElement)ProgressFill.Parent).ActualWidth;
        double total = _module.Total.TotalSeconds;
        double done = total <= 0 ? 0 : 1 - _module.Remaining.TotalSeconds / total;
        ProgressFill.Width = track * Math.Clamp(done, 0, 1);
    }

    /// <summary>番茄鐘:用 4 個小圓點表示這一組(到長休息前)完成幾輪。</summary>
    private void UpdateRounds()
    {
        int inSet = _module.CompletedRounds % TimerModule.RoundsPerLongBreak;
        if (_module.CompletedRounds > 0 && inSet == 0 && _module.Phase != PomodoroPhase.Focus) inSet = TimerModule.RoundsPerLongBreak;
        Rounds.Children.Clear();
        for (int i = 0; i < TimerModule.RoundsPerLongBreak; i++)
        {
            var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0) };
            dot.SetResourceReference(Border.BackgroundProperty, i < inSet ? IslandTheme.Accent : IslandTheme.AccentTrack);
            Rounds.Children.Add(dot);
        }
        Rounds.Children.Add(new TextBlock
        {
            Text = $"已完成 {_module.CompletedRounds} 輪 · 專注 25 分 / 休息 5 分",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        });
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        _module.SetMode(sender == PomodoroTab ? TimerMode.Pomodoro : TimerMode.Countdown);
    }

    private void OnPresetChecked(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not RadioButton { Tag: string tag }) return;
        _module.SetCountdownLength(TimeSpan.FromMinutes(int.Parse(tag)));
    }

    private void OnTimeWheel(object sender, MouseWheelEventArgs e)
    {
        _module.AdjustMinutes(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => _module.StartPause();
    private void OnResetClick(object sender, RoutedEventArgs e) => _module.Reset();
    private void OnSkipClick(object sender, RoutedEventArgs e) => _module.Skip();
}

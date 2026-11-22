using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Media;

public partial class MediaView : UserControl
{
    private readonly MediaModule _module;
    private readonly ImageBrush _artBrush = new() { Stretch = Stretch.UniformToFill };
    private readonly Brush _artPlaceholder;

    internal MediaView(MediaModule module)
    {
        _module = module;
        InitializeComponent();
        _artPlaceholder = Art.Background;
        // 進度條寬度跟著軌道寬度走
        ((FrameworkElement)ProgressFill.Parent).SizeChanged += (_, _) => UpdateProgress(_module.Current);
        Update(module.Current);
    }

    public void Update(MediaSnapshot s)
    {
        bool has = s.HasSession && (!string.IsNullOrEmpty(s.Title) || s.IsPlaying);
        Body.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        if (!has) return;

        TitleText.Text = string.IsNullOrEmpty(s.Title) ? "(未知的標題)" : s.Title;
        ArtistText.Text = s.Artist;
        AppText.Text = s.AppName;

        if (s.Thumbnail is not null)
        {
            _artBrush.ImageSource = s.Thumbnail;
            Art.Background = _artBrush;
            ArtFallback.Visibility = Visibility.Collapsed;
        }
        else
        {
            Art.Background = _artPlaceholder;
            ArtFallback.Visibility = Visibility.Visible;
        }

        PlayButton.Content = s.IsPlaying ? "\uE769" : "\uE768";   // 暫停 / 播放
        PlayButton.ToolTip = s.IsPlaying ? "暫停" : "播放";
        PlayButton.IsEnabled = s.CanPlayPause;
        PrevButton.IsEnabled = s.CanPrevious;
        NextButton.IsEnabled = s.CanNext;

        SeekArea.Cursor = s.CanSeek ? System.Windows.Input.Cursors.Hand : null;
        SeekArea.ToolTip = s.CanSeek ? "點一下跳到這個位置" : null;
        UpdateProgress(s);
    }

    public void UpdateProgress(MediaSnapshot s)
    {
        bool hasTimeline = s.Duration > TimeSpan.Zero;
        ProgressRow.Visibility = hasTimeline ? Visibility.Visible : Visibility.Collapsed;
        if (!hasTimeline) return;

        var pos = s.EstimatedPosition;
        PositionText.Text = Format(pos);
        DurationText.Text = Format(s.Duration);
        double track = ((FrameworkElement)ProgressFill.Parent).ActualWidth;
        ProgressFill.Width = track * Math.Clamp(pos.TotalSeconds / s.Duration.TotalSeconds, 0, 1);
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    // ── 音量 ─────────────────────────────────────────────

    private bool _settingVolume;   // 程式自己設定滑桿時,不要回寫到系統

    /// <summary>顯示目前的系統音量(展開時、或其他地方改了音量時呼叫)。</summary>
    public void ShowVolume(float level, bool muted, bool available)
    {
        VolumeRow.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (!available) return;
        _settingVolume = true;
        if (!VolumeSlider.IsMouseCaptureWithin) VolumeSlider.Value = Math.Round(level * 100);
        _settingVolume = false;
        UpdateVolumeLabel(muted);
    }

    private void UpdateVolumeLabel(bool muted)
    {
        int v = (int)Math.Round(VolumeSlider.Value);
        VolumeText.Text = muted ? "靜音" : $"{v}";
        MuteButton.Content = muted || v == 0 ? "\uE74F" : v < 34 ? "\uE993" : v < 67 ? "\uE994" : "\uE995";
        MuteButton.ToolTip = muted ? "取消靜音" : "靜音";
        VolumeSlider.Opacity = muted ? 0.45 : 1;
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settingVolume || !IsLoaded) return;
        bool muted = _module.SetVolume((float)(e.NewValue / 100));
        UpdateVolumeLabel(muted);
    }

    private void OnVolumeWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + (e.Delta > 0 ? 2 : -2), 0, 100);
        e.Handled = true;
    }

    private void OnMuteClick(object sender, RoutedEventArgs e) => UpdateVolumeLabel(_module.ToggleMute());

    private void OnSeekClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_module.Current.CanSeek || SeekArea.ActualWidth <= 0) return;
        double fraction = Math.Clamp(e.GetPosition(SeekArea).X / SeekArea.ActualWidth, 0, 1);
        _module.Seek(fraction);
        e.Handled = true;
    }

    private void OnPlayClick(object sender, RoutedEventArgs e) => _module.TogglePlayPause();
    private void OnNextClick(object sender, RoutedEventArgs e) => _module.Next();
    private void OnPrevClick(object sender, RoutedEventArgs e) => _module.Previous();
}

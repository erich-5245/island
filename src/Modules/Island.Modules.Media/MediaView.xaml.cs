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

        PlayButton.Content = s.IsPlaying ? "" : "";   // 暫停 / 播放
        PlayButton.ToolTip = s.IsPlaying ? "暫停" : "播放";
        PlayButton.IsEnabled = s.CanPlayPause;
        PrevButton.IsEnabled = s.CanPrevious;
        NextButton.IsEnabled = s.CanNext;

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

    private void OnPlayClick(object sender, RoutedEventArgs e) => _module.TogglePlayPause();
    private void OnNextClick(object sender, RoutedEventArgs e) => _module.Next();
    private void OnPrevClick(object sender, RoutedEventArgs e) => _module.Previous();
}

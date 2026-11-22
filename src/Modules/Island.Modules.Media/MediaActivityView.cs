using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Media;

/// <summary>
/// 播放中、閒置時顯示在小膠囊裡:[封面] 歌名 [♪]。
/// 沒有動畫(軟體算圖下持續動畫會一直耗 CPU)。
/// </summary>
internal sealed class MediaActivityView : Grid
{
    private readonly Border _art;
    private readonly ImageBrush _artBrush = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _glyphFallback;
    private readonly TextBlock _title;

    public MediaActivityView(MediaSnapshot initial)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        VerticalAlignment = VerticalAlignment.Center;

        _glyphFallback = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _art = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)),
            Child = _glyphFallback,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_art);

        _title = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 8, 0),
        };
        SetColumn(_title, 1);
        Children.Add(_title);

        var indicator = new TextBlock
        {
            Text = "",   // 音量圖示,表示「正在發出聲音」
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetColumn(indicator, 2);
        Children.Add(indicator);

        Update(initial);
    }

    public void Update(MediaSnapshot s)
    {
        _title.Text = string.IsNullOrEmpty(s.Artist) ? s.Title : $"{s.Title} · {s.Artist}";
        if (s.Thumbnail is not null)
        {
            _artBrush.ImageSource = s.Thumbnail;
            _art.Background = _artBrush;
            _art.Child = null;
        }
        else
        {
            _art.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            _art.Child = _glyphFallback;
        }
    }
}

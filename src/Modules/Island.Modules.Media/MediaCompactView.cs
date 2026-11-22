using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Island.Modules.Media;

/// <summary>小圓:圓形專輯封面(沒有封面時顯示音符)。</summary>
internal sealed class MediaCompactView : Grid
{
    private readonly Ellipse _art = new() { Width = 26, Height = 26 };
    private readonly ImageBrush _brush = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _fallback = new()
    {
        Text = "\uE8D6",
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontSize = 13,
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public MediaCompactView(MediaSnapshot initial)
    {
        Children.Add(_art);
        Children.Add(_fallback);
        Update(initial);
    }

    public void Update(MediaSnapshot s)
    {
        bool has = s.Thumbnail is not null;
        if (has) _brush.ImageSource = s.Thumbnail;
        _art.Fill = has ? _brush : null;
        _fallback.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
    }
}

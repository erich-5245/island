using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island.Modules.Shelf;

/// <summary>截圖放進 Shelf 時膠囊裡的一排:[縮圖] 截圖已放進 Shelf    拖出即可使用</summary>
internal sealed class ScreenshotToastView : Grid
{
    private readonly Border _thumb;
    private readonly ImageBrush _brush = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _detail;

    public ScreenshotToastView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        VerticalAlignment = VerticalAlignment.Center;

        _thumb = new Border
        {
            Width = 30,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            Background = _brush,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        Children.Add(_thumb);

        var title = new TextBlock
        {
            Text = "截圖已放進 Shelf",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetColumn(title, 1);
        Children.Add(title);

        _detail = new TextBlock
        {
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(10, 0, 2, 0),
        };
        SetColumn(_detail, 2);
        Children.Add(_detail);
    }

    public void Show(string path, int count)
    {
        _brush.ImageSource = LoadThumbnail(path);
        _detail.Text = count > 1 ? $"共 {count} 個 · 點開拖出" : "點開即可拖出";
    }

    private static ImageSource? LoadThumbnail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 80;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Weather;

/// <summary>下雨提醒時膠囊裡的一排:[☂] 30 分鐘內可能下雨   新竹市東區 · 記得帶傘</summary>
internal sealed class WeatherAlertView : Grid
{
    private readonly TextBlock _title;
    private readonly TextBlock _detail;

    public WeatherAlertView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        VerticalAlignment = VerticalAlignment.Center;

        var icon = new TextBlock
        {
            Text = "☂",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 9, 0),
        };
        icon.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        Children.Add(icon);

        _title = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetColumn(_title, 1);
        Children.Add(_title);

        _detail = new TextBlock
        {
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 2, 0),
        };
        SetColumn(_detail, 2);
        Children.Add(_detail);
    }

    public void Show(string title, string detail)
    {
        _title.Text = title;
        _detail.Text = detail;
    }
}

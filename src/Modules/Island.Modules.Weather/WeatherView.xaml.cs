using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Weather;

public partial class WeatherView : UserControl
{
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)));
    private static readonly Brush RainBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x7F, 0xB4, 0xE8)));
    private static readonly FontFamily SymbolFont = new("Segoe UI Symbol");

    private readonly WeatherModule _module;

    internal WeatherView(WeatherModule module)
    {
        _module = module;
        InitializeComponent();
        Update();
    }

    public void Update()
    {
        var s = _module.Current;
        Body.Visibility = s is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = s is null ? Visibility.Visible : Visibility.Collapsed;
        if (s is null)
        {
            EmptyText.Text = _module.Error ?? "正在取得天氣…";
            return;
        }

        NowSymbol.Text = WeatherCodes.Symbol(s.Code, s.IsDay);
        NowTemp.Text = $"{Math.Round(s.Temperature):0}°";
        NowDesc.Text = $"{WeatherCodes.Describe(s.Code)} · 體感 {Math.Round(s.FeelsLike):0}°";
        TodayText.Text = $"{Math.Round(s.TodayMin):0}° – {Math.Round(s.TodayMax):0}° · 降雨 {s.TodayRainChance}%";

        HoursGrid.Children.Clear();
        foreach (var h in s.Hours)
        {
            bool day = h.Time.Hour is >= 6 and < 18;
            var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            col.Children.Add(Text($"{h.Time.Hour} 時", 10.5, Muted));
            col.Children.Add(new TextBlock
            {
                Text = WeatherCodes.Symbol(h.Code, day),
                FontFamily = SymbolFont,
                FontSize = 15,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 2),
            });
            col.Children.Add(Text($"{Math.Round(h.Temperature):0}°", 12, Brushes.White));
            if (h.RainChance >= 20) col.Children.Add(Text($"{h.RainChance}%", 10, RainBrush));
            HoursGrid.Children.Add(col);
        }

        FooterText.Text = $"{_module.Location.Name} · {s.FetchedAt:HH:mm} 更新" + (_module.Error is null ? "" : $" · {_module.Error}");
    }

    private static TextBlock Text(string text, double size, Brush brush) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}

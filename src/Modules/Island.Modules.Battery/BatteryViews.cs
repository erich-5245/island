using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Island.Modules.Battery;

internal static class BatteryGlyphs
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x5C, 0xC4, 0x6A)));
    public static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x5F)));
    public static readonly Brush Track = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));

    /// <summary>Segoe 電池圖示:Battery0~9 = E850~E859、Battery10 = E83F;充電中 E85A~E862、E83E。</summary>
    public static string For(int percent, bool charging)
    {
        int step = Math.Clamp(percent / 10, 0, 10);
        if (charging) return step == 10 ? "\uE83E" : ((char)(0xE85A + step)).ToString();
        return step == 10 ? "\uE83F" : ((char)(0xE850 + step)).ToString();
    }

    /// <summary>插電 = 綠色、電量低 = 紅色、其他 = 白色。</summary>
    public static Brush BrushFor(BatteryModule m) => m.OnAc ? Green : m.IsLow ? Red : Brushes.White;

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }
}

/// <summary>提示的一排:[電池圖示] 充電中 ............ 78%</summary>
internal sealed class BatteryView : Grid
{
    private readonly BatteryModule _module;
    private readonly TextBlock _icon;
    private readonly TextBlock _message;
    private readonly TextBlock _percent;

    public BatteryView(BatteryModule module)
    {
        _module = module;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        VerticalAlignment = VerticalAlignment.Center;

        _icon = new TextBlock { FontFamily = BatteryGlyphs.IconFont, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 10, 0) };
        Children.Add(_icon);

        _message = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        SetColumn(_message, 1);
        Children.Add(_message);

        _percent = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0) };
        Typography.SetNumeralAlignment(_percent, FontNumeralAlignment.Tabular);
        SetColumn(_percent, 2);
        Children.Add(_percent);

        Update();
    }

    public void Update()
    {
        var brush = BatteryGlyphs.BrushFor(_module);
        _icon.Text = BatteryGlyphs.For(_module.Percent, _module.Charging || _module.OnAc);
        _icon.Foreground = brush;
        _message.Text = _module.Message;
        _percent.Text = $"{_module.Percent}%";
        _percent.Foreground = brush;
    }
}

/// <summary>小圓:電量環(插電綠色、偏低紅色)+ 中間的百分比。</summary>
internal sealed class BatteryRingView : Grid
{
    private const double Size = 28;
    private const double Thickness = 3;
    private static readonly double DashUnits = Math.PI * (Size - Thickness) / Thickness;

    private readonly BatteryModule _module;
    private readonly Ellipse _ring;
    private readonly TextBlock _text;

    public BatteryRingView(BatteryModule module)
    {
        _module = module;
        Width = Height = Size;
        Children.Add(new Ellipse { StrokeThickness = Thickness, Stroke = BatteryGlyphs.Track });
        _ring = new Ellipse
        {
            StrokeThickness = Thickness,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-90),
        };
        Children.Add(_ring);
        _text = new TextBlock
        {
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(_text, FontNumeralAlignment.Tabular);
        Children.Add(_text);
        Update();
    }

    public void Update()
    {
        double level = Math.Clamp(_module.Percent / 100.0, 0, 1);
        _ring.StrokeDashArray = new DoubleCollection { level * DashUnits, DashUnits + 1 };
        _ring.Stroke = BatteryGlyphs.BrushFor(_module);
        _text.Text = $"{_module.Percent}";
    }
}

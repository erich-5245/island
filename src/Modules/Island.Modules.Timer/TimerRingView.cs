using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Island.Core;

namespace Island.Modules.Timer;

/// <summary>小圓:剩餘時間的進度環 + 中間剩幾分鐘(不到 1 分鐘時顯示秒數)。</summary>
internal sealed class TimerRingView : Grid
{
    private const double Size = 28;
    private const double Thickness = 3;
    /// <summary>StrokeDashArray 以線寬為單位:圓周 / 線寬。</summary>
    private static readonly double DashUnits = Math.PI * (Size - Thickness) / Thickness;

    private readonly TimerModule _module;
    private readonly Ellipse _progress;
    private readonly TextBlock _text;

    public TimerRingView(TimerModule module)
    {
        _module = module;
        Width = Height = Size;

        var track = new Ellipse { StrokeThickness = Thickness };
        track.SetResourceReference(Shape.StrokeProperty, IslandTheme.AccentTrack);
        Children.Add(track);

        _progress = new Ellipse
        {
            StrokeThickness = Thickness,
            StrokeDashCap = PenLineCap.Flat,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-90),   // 從 12 點鐘方向開始
        };
        _progress.SetResourceReference(Shape.StrokeProperty, IslandTheme.Accent);
        Children.Add(_progress);

        _text = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(_text, FontNumeralAlignment.Tabular);
        Children.Add(_text);

        IsVisibleChanged += (_, _) => { if (IsVisible) Update(); };
        Update();
    }

    public void Update()
    {
        var remaining = _module.Remaining;
        double total = _module.Total.TotalSeconds;
        double left = total <= 0 ? 0 : Math.Clamp(remaining.TotalSeconds / total, 0, 1);
        // 剩餘的部分畫成實線(和 iPhone 計時器一樣,環會慢慢變短)
        _progress.StrokeDashArray = new DoubleCollection { left * DashUnits, DashUnits + 1 };

        if (_module.State == TimerState.Done) _text.Text = "!";
        else if (remaining.TotalSeconds < 60) _text.Text = $"{Math.Ceiling(remaining.TotalSeconds):0}";
        else _text.Text = $"{Math.Ceiling(remaining.TotalMinutes):0}";
        _progress.Opacity = _module.State == TimerState.Paused ? 0.45 : 1;
    }
}

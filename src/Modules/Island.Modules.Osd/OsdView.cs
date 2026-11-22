using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Osd;

/// <summary>[喇叭/太陽] ──────●──── 45</summary>
internal sealed class OsdView : Grid
{
    private readonly OsdModule _module;
    private readonly TextBlock _icon;
    private readonly Border _fill;
    private readonly Grid _track;
    private readonly TextBlock _value;

    public OsdView(OsdModule module)
    {
        _module = module;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        VerticalAlignment = VerticalAlignment.Center;

        _icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 15,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 12, 0),
        };
        Children.Add(_icon);

        _track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center };
        var back = new Border { CornerRadius = new CornerRadius(3) };
        back.SetResourceReference(Border.BackgroundProperty, IslandTheme.AccentTrack);
        _fill = new Border { CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        _fill.SetResourceReference(Border.BackgroundProperty, IslandTheme.Accent);
        _track.Children.Add(back);
        _track.Children.Add(_fill);
        _track.SizeChanged += (_, _) => UpdateFill();
        SetColumn(_track, 1);
        Children.Add(_track);

        _value = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
        SetColumn(_value, 2);
        Children.Add(_value);

        Update();
    }

    public void Update()
    {
        int percent = (int)Math.Round(_module.Level * 100);
        if (_module.Kind == OsdKind.Brightness)
        {
            _icon.Text = percent < 40 ? "\uEC8A" : "\uE706";   // 低亮度 / 太陽
        }
        else
        {
            _icon.Text = _module.Muted || percent == 0 ? "\uE74F"
                : percent < 34 ? "\uE993" : percent < 67 ? "\uE994" : "\uE995";
        }
        _value.Text = _module.Muted && _module.Kind == OsdKind.Volume ? "靜音" : percent.ToString();
        _value.FontSize = _value.Text.Length > 3 ? 11 : 12.5;
        _fill.Opacity = _module.Muted ? 0.35 : 1;
        UpdateFill();
    }

    private void UpdateFill() => _fill.Width = _track.ActualWidth * Math.Clamp(_module.Level, 0, 1);
}

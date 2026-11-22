using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Timer;

/// <summary>
/// 計時中滑鼠移過去、或時間到時跳出的一排:[碼錶/鈴鐺] 剩餘時間  狀態文字。
/// </summary>
internal sealed class TimerActivityView : Grid
{
    private readonly TimerModule _module;
    private readonly TextBlock _icon;
    private readonly TextBlock _time;
    private readonly TextBlock _status;

    public TimerActivityView(TimerModule module)
    {
        _module = module;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        VerticalAlignment = VerticalAlignment.Center;

        _icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 10, 0),
        };
        _icon.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        Children.Add(_icon);

        _time = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(_time, FontNumeralAlignment.Tabular);
        SetColumn(_time, 1);
        Children.Add(_time);

        _status = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 2, 0),
        };
        SetColumn(_status, 2);
        Children.Add(_status);

        Update();
        IsVisibleChanged += (_, _) => { if (IsVisible) Update(); };
    }

    public void Update()
    {
        bool done = _module.State == TimerState.Done;
        _icon.Text = done ? "\uEA8F" : "\uE916";   // 鈴鐺 / 碼錶
        _time.Text = done ? _module.DoneMessage : TimerModule.Format(_module.Remaining);
        _time.FontSize = done ? 13.5 : 15;
        _status.Text = done ? "點一下開啟" : _module.StatusText;
    }
}

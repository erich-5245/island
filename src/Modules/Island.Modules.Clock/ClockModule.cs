using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;
using Microsoft.Win32;

namespace Island.Modules.Clock;

/// <summary>
/// 時間模組。效能重點:只用一個「對齊到下一個整分鐘」的單次計時器,
/// 每分鐘醒來一次更新文字,其餘時間完全不佔 CPU。
/// 系統時間被修改或從睡眠喚醒時立即校正。
/// </summary>
public sealed class ClockModule : IslandModuleBase
{
    private const string Format = "HH:mm";

    /// <summary>過整分後再多等一點點,確保醒來時分鐘已經進位。</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(30);

    private DispatcherTimer? _timer;
    private TextBlock? _collapsedText;
    private TextBlock? _expandedTime;
    private TextBlock? _expandedDate;

    public override string Id => "clock";
    public override string Title => "時間";

    protected override void OnInitialize()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal, Host.Dispatcher);
        _timer.Tick += (_, _) => Refresh();
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Refresh();
    }

    public override FrameworkElement CreateCollapsedView()
    {
        _collapsedText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Text = DateTime.Now.ToString(Format, CultureInfo.InvariantCulture),
        };
        // 等寬數字,時間跳動時寬度不變
        Typography.SetNumeralAlignment(_collapsedText, FontNumeralAlignment.Tabular);
        return _collapsedText;
    }

    public override FrameworkElement CreateExpandedView()
    {
        // 第 1 階段外殼尚未使用,先提供基本內容
        _expandedTime = new TextBlock { FontSize = 40, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        Typography.SetNumeralAlignment(_expandedTime, FontNumeralAlignment.Tabular);
        _expandedDate = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)) };
        _expandedTime.HorizontalAlignment = HorizontalAlignment.Center;
        _expandedDate.HorizontalAlignment = HorizontalAlignment.Center;
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
        };
        panel.Children.Add(_expandedTime);
        panel.Children.Add(_expandedDate);
        UpdateText(DateTime.Now);
        return panel;
    }

    private void Refresh()
    {
        var now = DateTime.Now;
        UpdateText(now);

        // 重新排程到下一個整分。每次都重算,不會累積誤差
        var next = now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);
        _timer!.Stop();
        _timer.Interval = next - now + Slack;
        _timer.Start();
    }

    private void UpdateText(DateTime now)
    {
        var text = now.ToString(Format, CultureInfo.InvariantCulture);
        if (_collapsedText is not null && _collapsedText.Text != text) _collapsedText.Text = text;
        if (_expandedTime is not null) _expandedTime.Text = text;
        if (_expandedDate is not null) _expandedDate.Text = now.ToString("M月d日 dddd", CultureInfo.CurrentCulture);
    }

    private void OnTimeChanged(object? sender, EventArgs e) => Host.Dispatcher.BeginInvoke(Refresh);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Host.Dispatcher.BeginInvoke(Refresh);
    }

    public override void Dispose()
    {
        _timer?.Stop();
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        base.Dispose();
    }
}

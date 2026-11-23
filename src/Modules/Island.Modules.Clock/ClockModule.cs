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
public sealed class ClockModule : IslandModuleBase, IIslandPeekProvider, IIslandSettingsPage
{
    /// <summary>24 小時制 "14:05";12 小時制 "2:05"(展開畫面另外顯示上午 / 下午)。</summary>
    private string Format => _options.Use24Hour ? "HH:mm" : "h:mm";
    private ClockOptions _options = new();

    /// <summary>過整分後再多等一點點,確保醒來時分鐘已經進位。</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(30);

    private DispatcherTimer? _timer;
    private TextBlock? _collapsedText;
    private TextBlock? _expandedTime;
    private TextBlock? _expandedDate;
    private TextBlock? _peekDate;

    public override string Id => "clock";
    public override string Title => "時間";

    protected override void OnInitialize()
    {
        _options = ClockOptions.Load(Host.GetModuleDataDirectory(Id));
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

    /// <summary>滑鼠移到膠囊上時,左側顯示的日期。</summary>
    public FrameworkElement CreatePeekView()
    {
        _peekDate = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        UpdateText(DateTime.Now);
        return _peekDate;
    }

    public override FrameworkElement CreateExpandedView()
    {
        _expandedTime = new TextBlock { FontSize = 34, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        Typography.SetNumeralAlignment(_expandedTime, FontNumeralAlignment.Tabular);
        _expandedDate = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)) };
        _expandedTime.HorizontalAlignment = HorizontalAlignment.Center;
        _expandedDate.HorizontalAlignment = HorizontalAlignment.Center;
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6),
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
        if (_expandedTime is not null && !_options.Use24Hour) _expandedTime.Text = $"{AmPm(now)} {text}";
        if (_expandedDate is not null) _expandedDate.Text = now.ToString("M月d日 dddd", CultureInfo.CurrentCulture);
        if (_peekDate is not null) _peekDate.Text = _options.Use24Hour
            ? now.ToString("M/d ddd", CultureInfo.CurrentCulture)
            : $"{now.ToString("M/d ddd", CultureInfo.CurrentCulture)} {AmPm(now)}";
    }

    /// <summary>上午 / 下午(固定中文,不受系統語言影響)。</summary>
    internal static string AmPm(DateTime t) => t.Hour < 12 ? "上午" : "下午";

    // ── 設定頁 ──
    public string SettingsTitle => "時間";

    public FrameworkElement CreateSettingsView()
    {
        var card = new Border();
        card.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCard);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        var title = new TextBlock { Text = "24 小時制" };
        title.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsTitleText);
        var caption = new TextBlock { Text = "關閉後改成 12 小時制(2:05),滑鼠移過去的日期旁邊和展開畫面會顯示上午 / 下午。" };
        caption.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCaptionText);
        text.Children.Add(title);
        text.Children.Add(caption);
        grid.Children.Add(text);
        var toggle = new CheckBox { IsChecked = _options.Use24Hour, Margin = new Thickness(16, 0, 0, 0) };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsToggle);
        toggle.Checked += (_, _) => Set24(true);
        toggle.Unchecked += (_, _) => Set24(false);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        card.Child = grid;
        return card;
    }

    private void Set24(bool on)
    {
        _options.Use24Hour = on;
        _options.Save(Host.GetModuleDataDirectory(Id));
        UpdateText(DateTime.Now);
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

/// <summary>%LOCALAPPDATA%\Island\modules\clock\options.json</summary>
internal sealed class ClockOptions
{
    public bool Use24Hour { get; set; } = true;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public static ClockOptions Load(string dir)
    {
        try
        {
            var file = System.IO.Path.Combine(dir, "options.json");
            if (System.IO.File.Exists(file))
                return System.Text.Json.JsonSerializer.Deserialize<ClockOptions>(System.IO.File.ReadAllText(file), JsonOptions) ?? new();
        }
        catch { }
        return new();
    }

    public void Save(string dir)
    {
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "options.json"), System.Text.Json.JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { }
    }
}

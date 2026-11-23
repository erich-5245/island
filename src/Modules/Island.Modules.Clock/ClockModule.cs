using System.IO;
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

        // 背景圖(沒設定時看不見):圓角卡片 + 可調深淺的暗色遮罩,讓白字看得清楚
        _bgImage = new Border { CornerRadius = new CornerRadius(14), Visibility = Visibility.Collapsed };
        _bgDim = new Border { CornerRadius = new CornerRadius(14), Background = Brushes.Black, Visibility = Visibility.Collapsed };
        var grid = new Grid();
        grid.Children.Add(_bgImage);
        grid.Children.Add(_bgDim);
        grid.Children.Add(panel);
        ApplyBackground();
        return grid;
    }

    private Border? _bgImage, _bgDim;

    private string BackgroundPath => Path.Combine(Host.GetModuleDataDirectory(Id), _options.BackgroundFile ?? "");

    /// <summary>依設定套用 / 移除背景圖。只在打開分頁前和改設定時執行。</summary>
    private void ApplyBackground()
    {
        if (_bgImage is null || _bgDim is null || _expandedTime is null || _expandedDate is null) return;
        var image = string.IsNullOrEmpty(_options.BackgroundFile) ? null : ClockBackground.Load(BackgroundPath, _options.BackgroundBlur);
        if (image is null)
        {
            _bgImage.Visibility = _bgDim.Visibility = Visibility.Collapsed;
            _bgImage.Background = null;
            _expandedTime.Effect = _expandedDate.Effect = null;
            _expandedDate.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
            return;
        }
        _bgImage.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        _bgDim.Opacity = _options.BackgroundDim;
        _bgImage.Visibility = _bgDim.Visibility = Visibility.Visible;
        // 圖片上的字:日期改亮一點,加淡淡陰影(文字一分鐘才變一次,成本可忽略)
        _expandedDate.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        var shadow = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Opacity = 0.6 };
        _expandedTime.Effect = shadow;
        _expandedDate.Effect = shadow;
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
        var page = new StackPanel();
        page.Children.Add(HourCard());
        page.Children.Add(BackgroundCard());
        return page;
    }

    private static readonly DependencyProperty StyleProp = FrameworkElement.StyleProperty;

    private FrameworkElement BackgroundCard()
    {
        var card = new Border();
        card.SetResourceReference(StyleProp, IslandTheme.SettingsCard);
        var p = new StackPanel();
        var title = new TextBlock { Text = "「時間」分頁的背景圖" };
        title.SetResourceReference(StyleProp, IslandTheme.SettingsTitleText);
        var caption = new TextBlock { Text = "選一張自己的圖片當展開後「時間」分頁的背景。圖片會複製一份保存,原檔之後搬走也沒關係。動態圖只會顯示第一格。" };
        caption.SetResourceReference(StyleProp, IslandTheme.SettingsCaptionText);
        p.Children.Add(title);
        p.Children.Add(caption);

        // 預覽(和分頁一樣的比例與暗色遮罩)
        var preview = new Border { Width = 228, Height = 100, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)) };
        var previewDim = new Border { CornerRadius = new CornerRadius(10), Background = Brushes.Black };
        var previewTime = new TextBlock { Text = DateTime.Now.ToString(Format, CultureInfo.InvariantCulture), FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        preview.Child = new Grid { Children = { previewDim, previewTime } };
        p.Children.Add(preview);

        void UpdatePreview()
        {
            var img = string.IsNullOrEmpty(_options.BackgroundFile) ? null : ClockBackground.Load(BackgroundPath, _options.BackgroundBlur);
            preview.Background = img is null ? new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)) : new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            previewDim.Opacity = img is null ? 0 : _options.BackgroundDim;
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var pick = new Button { Content = "選擇圖片…" };
        pick.SetResourceReference(StyleProp, IslandTheme.SettingsButton);
        var remove = new Button { Content = "移除", Margin = new Thickness(8, 0, 0, 0), IsEnabled = !string.IsNullOrEmpty(_options.BackgroundFile) };
        remove.SetResourceReference(StyleProp, IslandTheme.SettingsButton);
        buttons.Children.Add(pick);
        buttons.Children.Add(remove);
        p.Children.Add(buttons);

        pick.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "選擇背景圖片",
                Filter = "圖片 (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp",
            };
            if (dlg.ShowDialog() != true || !ClockBackground.IsSupported(dlg.FileName)) return;
            if (ClockBackground.Load(dlg.FileName, blur: false) is null)
            {
                MessageBox.Show("這張圖片讀不出來,請換一張。", "Island");
                return;
            }
            try { _options.BackgroundFile = ClockBackground.Import(dlg.FileName, Host.GetModuleDataDirectory(Id)); }
            catch (Exception ex) { Host.Log($"時間:背景圖複製失敗:{ex.Message}"); return; }
            SaveOptions();
            remove.IsEnabled = true;
            UpdatePreview();
        };
        remove.Click += (_, _) =>
        {
            _options.BackgroundFile = null;
            SaveOptions();
            ClockBackground.Remove(Host.GetModuleDataDirectory(Id));
            remove.IsEnabled = false;
            UpdatePreview();
        };

        // 暗度
        var dimRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var dimLabel = new TextBlock { Text = "暗度", Width = 48, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontSize = 12.5 };
        var dimValue = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontSize = 12.5 };
        DockPanel.SetDock(dimLabel, Dock.Left);
        DockPanel.SetDock(dimValue, Dock.Right);
        var slider = new Slider { Minimum = 0, Maximum = 80, Value = _options.BackgroundDim * 100, Width = 220, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        if (Application.Current.TryFindResource("Settings.Slider") is Style sliderStyle) slider.Style = sliderStyle;
        dimValue.Text = $"{slider.Value:0}%";
        slider.ValueChanged += (_, e) =>
        {
            _options.BackgroundDim = Math.Round(e.NewValue) / 100;
            dimValue.Text = $"{Math.Round(e.NewValue):0}%";
            previewDim.Opacity = string.IsNullOrEmpty(_options.BackgroundFile) ? 0 : _options.BackgroundDim;
            SaveOptions(reload: false);
            if (_bgDim is not null) _bgDim.Opacity = _options.BackgroundDim;
        };
        dimRow.Children.Add(dimLabel);
        dimRow.Children.Add(dimValue);
        dimRow.Children.Add(slider);
        p.Children.Add(dimRow);

        // 模糊
        var blurRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var blurToggle = new CheckBox { IsChecked = _options.BackgroundBlur };
        blurToggle.SetResourceReference(StyleProp, IslandTheme.SettingsToggle);
        DockPanel.SetDock(blurToggle, Dock.Right);
        blurRow.Children.Add(blurToggle);
        blurRow.Children.Add(new TextBlock { Text = "模糊(字比較清楚)", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, FontSize = 12.5 });
        blurToggle.Checked += (_, _) => { _options.BackgroundBlur = true; SaveOptions(); UpdatePreview(); };
        blurToggle.Unchecked += (_, _) => { _options.BackgroundBlur = false; SaveOptions(); UpdatePreview(); };
        p.Children.Add(blurRow);

        UpdatePreview();
        card.Child = p;
        return card;
    }

    private void SaveOptions(bool reload = true)
    {
        _options.Save(Host.GetModuleDataDirectory(Id));
        if (reload) ApplyBackground();
    }

    private FrameworkElement HourCard()
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

    /// <summary>「時間」分頁背景圖的檔名(在 modules\clock 裡);null = 不用。</summary>
    public string? BackgroundFile { get; set; }

    /// <summary>背景圖上的暗色遮罩 0–0.8。</summary>
    public double BackgroundDim { get; set; } = 0.4;

    public bool BackgroundBlur { get; set; }

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
            {
                var o = System.Text.Json.JsonSerializer.Deserialize<ClockOptions>(System.IO.File.ReadAllText(file), JsonOptions) ?? new();
                o.BackgroundDim = double.IsFinite(o.BackgroundDim) ? Math.Clamp(o.BackgroundDim, 0, 0.8) : 0.4;
                if (o.BackgroundFile is { } f && (f.Contains('/') || f.Contains('\\') || f.Contains(".."))) o.BackgroundFile = null;   // 只接受資料夾裡的檔名
                return o;
            }
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

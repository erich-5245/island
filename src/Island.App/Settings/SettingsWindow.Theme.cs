using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Island.App.Infrastructure;
using Island.App.Interop;
using Island.Core;

namespace Island.App.Settings;

/// <summary>
/// 「主題」頁:預設主題、強調色來源、膠囊底色、外型、字型、動畫速度、我的主題、匯出 / 匯入。
/// 每個改動立即套用到島上並存檔。
/// </summary>
public partial class SettingsWindow
{
    private ThemeSettings Theme => _settings.Theme;

    private UIElement BuildTheme()
    {
        var p = new StackPanel();
        p.Children.Add(Caption("改什麼都會立即套用到島上。按「在島上預覽」可以讓島出現幾秒看看效果。"));
        var preview = new Button { Content = "在島上預覽", Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)FindResource(IslandTheme.SettingsAccentButton) };
        preview.Click += (_, _) => _main.PreviewTheme();
        p.Children.Add(preview);

        // ── 預設主題 + 我的主題 ──
        p.Children.Add(Section("主題"));
        var presets = new WrapPanel();
        foreach (var preset in ThemeSettings.Presets) presets.Children.Add(ThemeTile(preset, deletable: false));
        foreach (var mine in _settings.CustomThemes) presets.Children.Add(ThemeTile(mine, deletable: true));
        p.Children.Add(new Border { Style = (Style)FindResource(IslandTheme.SettingsCard), Child = presets });

        // ── 強調色 ──
        p.Children.Add(Section("強調色"));
        var sources = new StackPanel();
        AddSource(sources, "跟著專輯封面", "播音樂時用封面的主色,沒在播放時是黑白。", AccentSource.Album);
        AddSource(sources, "跟著 Windows 主題色", "Windows 設定 → 個人化 → 色彩 改了,島也跟著改。", AccentSource.Windows);
        AddSource(sources, "固定顏色", "從下面的色盤挑一個,或自訂。", AccentSource.Fixed);
        AddSource(sources, "依時段變化", "清晨橘、白天藍、傍晚紫、深夜靛,每 15 分鐘慢慢漸變。", AccentSource.TimeOfDay);
        AddSource(sources, "不使用強調色", "只有黑白灰。", AccentSource.None);
        p.Children.Add(Card("強調色從哪裡來", "進度條、分頁、按鈕和邊框會用這個顏色。", sources, stacked: true));
        p.Children.Add(Card("固定顏色", "選「固定顏色」時使用。",
            ColorRow(ThemeSettings.AccentSwatches, () => Theme.AccentColor, hex =>
            {
                Theme.AccentColor = hex;
                Theme.AccentSource = AccentSource.Fixed;
            }), stacked: true));

        // ── 膠囊 ──
        p.Children.Add(Section("膠囊"));
        p.Children.Add(Card("底色", "建議用深色,文字是白色的。", ColorRow(ThemeSettings.CapsuleSwatches, () => Theme.CapsuleColor, hex => Theme.CapsuleColor = hex), stacked: true));
        p.Children.Add(SliderCard("不透明度", "調低會有半透明的玻璃感。", ThemeSettings.MinOpacity * 100, 100, 1, Theme.CapsuleOpacity * 100, "%",
            v => Change(() => Theme.CapsuleOpacity = v / 100)));
        p.Children.Add(SliderCard("圓角", "100% 是膠囊,0% 是圓角方形。", 0, 100, 1, Theme.Roundness * 100, "%",
            v => Change(() => Theme.Roundness = v / 100)));
        p.Children.Add(ToggleCard("細邊框", "膠囊與小圓外圍的 1px 邊框(有強調色時帶色調)。", Theme.ShowBorder,
            on => Change(() => Theme.ShowBorder = on)));

        // ── 文字與動畫 ──
        p.Children.Add(Section("文字與動畫"));
        p.Children.Add(Card("字型", "島上的文字(圖示不受影響)。", FontBox()));
        var speeds = new StackPanel { Orientation = Orientation.Horizontal };
        AddSpeed(speeds, "快", AnimationSpeed.Fast);
        AddSpeed(speeds, "標準", AnimationSpeed.Normal);
        AddSpeed(speeds, "慢", AnimationSpeed.Slow);
        p.Children.Add(Card("動畫速度", "出現、展開、收起的速度。", speeds));

        // ── 我的主題 / 分享 ──
        p.Children.Add(Section("我的主題與分享"));
        var nameBox = new TextBox { Width = 160, Text = "", Style = (Style)FindResource(IslandTheme.SettingsTextBox) };
        var save = new Button { Content = "存成我的主題", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsButton) };
        save.Click += (_, _) => SaveAsMine(nameBox.Text);
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal };
        saveRow.Children.Add(nameBox);
        saveRow.Children.Add(save);
        p.Children.Add(Card("存成我的主題", "取個名字,之後在上面的主題清單一鍵切換。", saveRow));

        var share = new StackPanel { Orientation = Orientation.Horizontal };
        var export = new Button { Content = "匯出…", Style = (Style)FindResource(IslandTheme.SettingsButton) };
        export.Click += (_, _) => ExportTheme();
        var import = new Button { Content = "匯入…", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsButton) };
        import.Click += (_, _) => ImportTheme();
        share.Children.Add(export);
        share.Children.Add(import);
        p.Children.Add(Card("匯出 / 匯入", "把目前的主題存成 .json 檔分享給別人,或匯入別人的主題。", share));
        return p;

        void AddSource(StackPanel panel, string label, string caption, AccentSource value)
        {
            var rb = new RadioButton
            {
                Content = new TextBlock { Inlines = { new System.Windows.Documents.Run(label), new System.Windows.Documents.Run("  " + caption) { Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)), FontSize = 11 } } },
                GroupName = "AccentSource",
                Style = (Style)FindResource("Settings.Choice"),
                IsChecked = Theme.AccentSource == value,
            };
            rb.Checked += (_, _) => Change(() => Theme.AccentSource = value, rebuild: false);
            panel.Children.Add(rb);
        }

        void AddSpeed(StackPanel panel, string label, AnimationSpeed value)
        {
            var rb = new RadioButton
            {
                Content = label,
                GroupName = "AnimSpeed",
                Margin = new Thickness(0, 0, 14, 0),
                Style = (Style)FindResource("Settings.Choice"),
                IsChecked = Theme.AnimationSpeed == value,
            };
            rb.Checked += (_, _) => Change(() => Theme.AnimationSpeed = value, rebuild: false);
            panel.Children.Add(rb);
        }
    }

    /// <summary>改主題 → 存檔 → 套用到島上。rebuild = 重畫這一頁(更新目前主題的標示、色盤勾選)。</summary>
    private void Change(Action change, bool rebuild = false)
    {
        change();
        Theme.Name = MatchName();
        Theme.Normalize();
        Save();
        _main.ApplyTheme();
        if (rebuild) Show("主題");
    }

    /// <summary>外觀和某個預設 / 我的主題完全一樣時用它的名字,否則叫「自訂」。</summary>
    private string MatchName() =>
        ThemeSettings.Presets.Concat(_settings.CustomThemes).FirstOrDefault(t => t.LooksLike(Theme))?.Name ?? "自訂";

    private void ApplyWhole(ThemeSettings t)
    {
        var copy = t.Clone();
        copy.Normalize();
        _settings.Theme = copy;
        Save();
        _main.ApplyTheme();
        _main.PreviewTheme();
        Show("主題");
    }

    // ── 主題小卡 ──

    private FrameworkElement ThemeTile(ThemeSettings t, bool deletable)
    {
        bool current = t.LooksLike(Theme);
        ThemeSettings.TryParseColor(t.CapsuleColor, out var capsule);
        Color accent = t.AccentSource switch
        {
            AccentSource.Fixed when ThemeSettings.TryParseColor(t.AccentColor, out var a) => a,
            AccentSource.TimeOfDay => ThemeSettings.TimeOfDayAccent(DateTime.Now),
            AccentSource.Windows => Color.FromRgb(0x0A, 0x84, 0xFF),
            AccentSource.Album => Color.FromRgb(0xE0, 0x7A, 0x5F),
            _ => Colors.White,
        };

        // 迷你膠囊:底色 + 強調色的進度條 + 時間
        double r = t.CollapsedRadius(26) * (26.0 / 26);
        var pill = new Border
        {
            Width = 92,
            Height = 26,
            CornerRadius = new CornerRadius(r),
            Background = new SolidColorBrush(Color.FromArgb((byte)(255 * t.CapsuleOpacity), capsule.R, capsule.G, capsule.B)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x48)),
            BorderThickness = new Thickness(t.ShowBorder ? 1 : 0),
        };
        var inner = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        inner.Children.Add(new TextBlock { Text = "12:34", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        inner.Children.Add(new Border
        {
            Width = 34,
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = new SolidColorBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });
        pill.Child = inner;

        var label = new TextBlock
        {
            Text = t.Name,
            FontSize = 11.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = current ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal,
        };
        var stack = new StackPanel();
        stack.Children.Add(pill);
        stack.Children.Add(label);

        var tile = new Border
        {
            Padding = new Thickness(8, 8, 8, 6),
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Background = current ? new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x36)) : Brushes.Transparent,
            BorderBrush = current ? new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5E)) : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            ToolTip = deletable ? $"{t.Name}(右鍵刪除)" : t.Name,
            Child = stack,
        };
        tile.MouseEnter += (_, _) => { if (!current) tile.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)); };
        tile.MouseLeave += (_, _) => { if (!current) tile.Background = Brushes.Transparent; };
        tile.MouseLeftButtonUp += (_, _) => ApplyWhole(t);
        if (deletable)
        {
            var menu = new ContextMenu();
            var del = new MenuItem { Header = "刪除這個主題" };
            del.Click += (_, _) =>
            {
                _settings.CustomThemes.Remove(t);
                Save();
                Show("主題");
            };
            menu.Items.Add(del);
            tile.ContextMenu = menu;
        }
        return tile;
    }

    // ── 色盤 ──

    /// <summary>一排色塊 + 色碼輸入框 + 「自訂…」(Windows 色彩對話方塊)。</summary>
    private FrameworkElement ColorRow(IReadOnlyList<string> swatches, Func<string> get, Action<string> set)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var row = new WrapPanel();
        foreach (var hex in swatches)
        {
            ThemeSettings.TryParseColor(hex, out var c);
            bool selected = string.Equals(hex, get(), StringComparison.OrdinalIgnoreCase);
            var sw = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(c),
                BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Cursor = Cursors.Hand,
                ToolTip = hex,
            };
            var value = hex;
            sw.MouseLeftButtonUp += (_, _) => Change(() => set(value), rebuild: true);
            row.Children.Add(sw);
        }
        panel.Children.Add(row);

        var edit = new StackPanel { Orientation = Orientation.Horizontal };
        var box = new TextBox { Width = 90, Text = get(), Style = (Style)FindResource(IslandTheme.SettingsTextBox) };
        void Commit()
        {
            if (ThemeSettings.TryParseColor(box.Text, out var c)) Change(() => set(ThemeSettings.ToHex(c)), rebuild: true);
            else box.Text = get();
        }
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        box.LostKeyboardFocus += (_, _) => { if (!string.Equals(box.Text, get(), StringComparison.OrdinalIgnoreCase)) Commit(); };
        var pick = new Button { Content = "自訂…", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsButton) };
        pick.Click += (_, _) =>
        {
            ThemeSettings.TryParseColor(get(), out var initial);
            if (IslandColorPicker.Pick(this, initial) is { } c) Change(() => set(ThemeSettings.ToHex(c)), rebuild: true);
        };
        edit.Children.Add(box);
        edit.Children.Add(pick);
        panel.Children.Add(edit);
        return panel;
    }

    // ── 字型 ──

    private ComboBox FontBox()
    {
        var box = new ComboBox { Width = 220, MaxDropDownHeight = 360 };
        box.Items.Add(new ComboBoxItem { Content = "系統預設", Tag = "" });
        var lang = XmlLanguage.GetLanguage("zh-tw");
        var families = Fonts.SystemFontFamilies
            .Select(f => (Source: f.Source, Name: f.FamilyNames.TryGetValue(lang, out var n) ? n : f.Source))
            .Where(f => !f.Source.Contains("Icons", StringComparison.OrdinalIgnoreCase) && !f.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)
                        && !f.Source.Contains("Symbol", StringComparison.OrdinalIgnoreCase) && !f.Source.Contains("Emoji", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(f => f.Source)
            .OrderBy(f => f.Name, StringComparer.CurrentCulture);
        foreach (var (source, name) in families)
            box.Items.Add(new ComboBoxItem { Content = name, Tag = source, FontFamily = new FontFamily(source) });
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, Theme.FontFamily, StringComparison.OrdinalIgnoreCase))
                           ?? box.Items[0];
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: string source }) Change(() => Theme.FontFamily = source);
        };
        return box;
    }

    // ── 我的主題 / 匯出匯入 ──

    private void SaveAsMine(string name)
    {
        var t = Theme.Clone();
        t.Name = string.IsNullOrWhiteSpace(name) ? $"我的主題 {_settings.CustomThemes.Count + 1}" : name.Trim();
        t.Normalize();
        _settings.CustomThemes.RemoveAll(x => string.Equals(x.Name, t.Name, StringComparison.Ordinal));
        _settings.CustomThemes.Add(t);
        _settings.Theme.Name = t.Name;
        Save();
        Show("主題");
    }

    private void ExportTheme()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "匯出主題",
            FileName = $"Island 主題 - {Theme.Name}.json",
            Filter = "Island 主題 (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try { File.WriteAllText(dlg.FileName, Theme.ToJson()); }
        catch (Exception ex) { IslandDialog.Show(this, $"匯出失敗:{ex.Message}"); }
    }

    private void ImportTheme()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "匯入主題", Filter = "Island 主題 (*.json)|*.json|所有檔案 (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        ThemeSettings? t = null;
        try
        {
            if (new FileInfo(dlg.FileName).Length < 64 * 1024) t = ThemeSettings.FromJson(File.ReadAllText(dlg.FileName));
        }
        catch (Exception ex) { Log.Write($"匯入主題失敗:{ex.Message}"); }
        if (t is null)
        {
            IslandDialog.Show(this, "這個檔案不是 Island 的主題。");
            return;
        }
        _settings.CustomThemes.RemoveAll(x => string.Equals(x.Name, t.Name, StringComparison.Ordinal));
        _settings.CustomThemes.Add(t);
        ApplyWhole(t);
    }
}

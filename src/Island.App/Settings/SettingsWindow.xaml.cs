using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Island.App.Infrastructure;
using System.Windows.Input;
using Island.App.Interop;
using Island.App.Modules;
using Island.Core;

namespace Island.App.Settings;

/// <summary>
/// 設定視窗。大部分開關立即生效並存檔;尺寸、模組開關等需要重新建立視窗的項目,
/// 底部會出現「重新啟動 Island」。模組可以透過 <see cref="IIslandSettingsPage"/> 加自己的頁面。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IslandSettings _settings;
    private readonly MainWindow _main;
    private readonly IReadOnlyList<IIslandModule> _modules;
    private readonly List<(string Title, Func<UIElement> Build)> _pages = [];

    internal SettingsWindow(IslandSettings settings, MainWindow main, IReadOnlyList<IIslandModule> modules)
    {
        _settings = settings;
        _main = main;
        _modules = modules;
        InitializeComponent();

        _pages.Add(("一般", BuildGeneral));
        _pages.Add(("外觀與位置", BuildDisplay));
        _pages.Add(("主題", BuildTheme));
        _pages.Add(("即時狀態", BuildLive));
        _pages.Add(("模組", BuildModules));
        foreach (var m in modules.OfType<IIslandSettingsPage>())
        {
            var page = m;
            _pages.Add((page.SettingsTitle, () => SafeBuild(page)));
        }

        foreach (var (title, _) in _pages)
        {
            var item = new RadioButton { Content = title, GroupName = "Nav", Style = (Style)FindResource("Settings.NavItem") };
            item.Checked += (_, _) => Show(title);
            Nav.Children.Add(item);
        }
        ((RadioButton)Nav.Children[1]).IsChecked = true;   // [0] 是標題文字
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 深色標題列(Windows 11)
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void Show(string title)
    {
        var page = _pages.First(p => p.Title == title);
        Page.Children.Clear();
        Page.Children.Add(Heading(title));
        Page.Children.Add(page.Build());
        Scroller.ScrollToTop();
    }

    private static UIElement SafeBuild(IIslandSettingsPage page)
    {
        try { return page.CreateSettingsView(); }
        catch (Exception ex)
        {
            Log.Write($"設定頁建立失敗 {page.SettingsTitle}:{ex.Message}");
            return Caption("這一頁暫時無法顯示。");
        }
    }

    // ── 一般 ────────────────────────────────────────────

    private UIElement BuildGeneral()
    {
        var p = new StackPanel();
        p.Children.Add(ToggleCard("開機時啟動", "登入 Windows 後自動執行(不需要系統管理員權限)。",
            AutoStart.IsEnabled, on =>
            {
                try { AutoStart.Set(on); } catch (Exception ex) { Log.Write($"設定開機自動啟動失敗:{ex.Message}"); }
            }));
        p.Children.Add(ToggleCard("閒置時隱藏", "平常完全隱藏,滑鼠移到螢幕上緣中間才出現。關閉後平常顯示只有時間的小膠囊。",
            _settings.HideWhenIdle, on => { _settings.HideWhenIdle = on; Save(); _main.ApplySettingsLive(); }));
        p.Children.Add(ToggleCard("全螢幕時隱藏", "看影片、玩遊戲、簡報全螢幕時自動隱藏。",
            _settings.HideInFullscreen, on => { _settings.HideInFullscreen = on; Save(); _main.ApplySettingsLive(); }));
        p.Children.Add(ToggleCard("切歌時顯示", "換歌時膠囊出現約 3 秒,顯示封面和歌名。",
            _settings.AnnounceActivity, on => { _settings.AnnounceActivity = on; Save(); }));
        p.Children.Add(ToggleCard("音量亮度提示", "調音量或亮度時顯示長條。",
            _settings.ShowOsd, on => { _settings.ShowOsd = on; Save(); }));
        p.Children.Add(HotkeyCard());
        p.Children.Add(ToggleCard("Ctrl+Alt+1…9 切換捷徑", "不用打開島,按 Ctrl+Alt+數字直接切到第幾個捷徑(順序在設定 →「捷徑」調整)。",
            _settings.NumberHotkeys, on => { _settings.NumberHotkeys = on; Save(); _main.ApplyNumberHotkeys(); }));
        return p;
    }

    // ── 外觀與位置 ──────────────────────────────────────

    private UIElement BuildDisplay()
    {
        var p = new StackPanel();

        // 螢幕
        var choices = new StackPanel();
        AddChoice(choices, "跟著 Windows 主螢幕", "primary");
        var monitors = MonitorInfo.GetAll();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            var label = $"螢幕 {i + 1} · {m.Bounds.Width}×{m.Bounds.Height}{(m.IsPrimary ? " · 目前的主螢幕" : "")}";
            AddChoice(choices, label, m.DeviceName);
        }
        p.Children.Add(Card("顯示在哪個螢幕", "選「跟著主螢幕」時,外接螢幕拔掉會自動移到筆電螢幕。", choices, stacked: true));

        p.Children.Add(Section("大小(拖動時立即套用)"));
        p.Children.Add(SliderCard("與螢幕頂端的距離", "", 0, 40, 1, _settings.TopOffset, "px",
            v => { _settings.TopOffset = v; SaveAndApply(); }));
        p.Children.Add(SliderCard("平常的膠囊寬度", "閒置時(不隱藏)只有時間的膠囊。", 120, 320, 10, _settings.CollapsedWidth, "",
            v => { _settings.CollapsedWidth = v; SaveAndApply(); }));
        p.Children.Add(SliderCard("膠囊高度", "", 28, 48, 1, _settings.CollapsedHeight, "",
            v => { _settings.CollapsedHeight = v; SaveAndApply(); }));
        p.Children.Add(SliderCard("滑鼠移過去時的寬度", "一排字(或音樂、計時)的長條。", 240, 480, 10, _settings.PeekWidth, "",
            v => { _settings.PeekWidth = v; SaveAndApply(); }));
        p.Children.Add(SliderCard("展開寬度", "", 320, 600, 10, _settings.ExpandedWidth, "",
            v => { _settings.ExpandedWidth = v; SaveAndApply(); }));
        p.Children.Add(SliderCard("展開高度", "", 150, 320, 5, _settings.ExpandedHeight, "",
            v => { _settings.ExpandedHeight = v; SaveAndApply(); }));

        p.Children.Add(Section("進階"));
        p.Children.Add(ToggleCard("軟體算圖", "省記憶體(約少一半)。如果動畫不夠順可以關掉改用 GPU。重新啟動後套用。",
            _settings.SoftwareRendering, on => { _settings.SoftwareRendering = on; Save(); NeedRestart(); }));
        return p;

        void AddChoice(StackPanel panel, string label, string value)
        {
            var rb = new RadioButton
            {
                Content = label,
                GroupName = "Display",
                Style = (Style)FindResource("Settings.Choice"),
                IsChecked = string.Equals(_settings.Display, value, StringComparison.OrdinalIgnoreCase),
            };
            rb.Checked += (_, _) =>
            {
                _settings.Display = value;
                Save();
                _main.ApplySettingsLive();
            };
            panel.Children.Add(rb);
        }
    }

    // ── 即時狀態 ────────────────────────────────────────

    private UIElement BuildLive()
    {
        var p = new StackPanel();
        p.Children.Add(Caption("滑鼠移到島上時,排最前面而且正在進行的(例如正在播放)會放在中間長條,其他有東西可看的依序放進兩側小圓(最多兩個)。"));
        p.Children.Add(new Border { Height = 10 });

        var candidates = _modules.Where(m => m is IIslandCompact or IIslandActivity && m.Id is not "osd").ToList();
        var order = _settings.LiveOrder
            .Where(id => candidates.Any(m => Same(m.Id, id)))
            .Concat(candidates.Select(m => m.Id).Where(id => !_settings.LiveOrder.Any(x => Same(x, id))))
            .ToList();

        for (int i = 0; i < order.Count; i++)
        {
            var id = order[i];
            var module = candidates.First(m => Same(m.Id, id));
            int index = i;

            var row = new DockPanel();
            var rank = new TextBlock
            {
                Text = (i + 1).ToString(),
                Width = 22,
                FontSize = 13,
                Foreground = (Brush)FindResource("Settings.Secondary"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(rank, Dock.Left);
            row.Children.Add(rank);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(controls, Dock.Right);
            var up = SmallButton("\uE70E", "往前", index > 0, () => Move(order, index, -1));
            var down = SmallButton("\uE70D", "往後", index < order.Count - 1, () => Move(order, index, +1));
            controls.Children.Add(up);
            controls.Children.Add(down);
            if (module is IIslandCompact compact && !string.IsNullOrEmpty(compact.CompactActionLabel))
            {
                // 點小圓:打開分頁 / 直接執行
                var action = new ComboBox { Width = 150, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "點小圓時" };
                action.Items.Add(new ComboBoxItem { Content = "點了打開分頁", Tag = CircleActions.Tab });
                action.Items.Add(new ComboBoxItem { Content = "點了" + compact.CompactActionLabel, Tag = CircleActions.Action });
                action.SelectedIndex = CircleActions.UseAction(_settings.CircleActions, id, compact) ? 1 : 0;
                action.SelectionChanged += (_, _) =>
                {
                    if (action.SelectedItem is not ComboBoxItem { Tag: string choice }) return;
                    foreach (var key in _settings.CircleActions.Keys.Where(k => Same(k, id)).ToList()) _settings.CircleActions.Remove(key);
                    _settings.CircleActions[id] = choice;
                    Save();
                };
                controls.Children.Add(action);
            }
            if (module is IIslandCompact)
            {
                controls.Children.Add(new TextBlock
                {
                    Text = "小圓",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("Settings.Secondary"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 8, 0),
                });
                controls.Children.Add(Toggle(!_settings.LiveHidden.Any(x => Same(x, id)), on =>
                {
                    _settings.LiveHidden.RemoveAll(x => Same(x, id));
                    if (!on) _settings.LiveHidden.Add(id);
                    Save();
                }));
            }
            row.Children.Add(controls);
            row.Children.Add(new TextBlock { Text = module.Title, Style = (Style)FindResource(IslandTheme.SettingsTitleText), VerticalAlignment = VerticalAlignment.Center });

            p.Children.Add(new Border { Style = (Style)FindResource(IslandTheme.SettingsCard), Child = row, Padding = new Thickness(14, 8, 14, 8) });
        }
        return p;

        void Move(List<string> list, int index, int delta)
        {
            int target = index + delta;
            if (target < 0 || target >= list.Count) return;
            (list[index], list[target]) = (list[target], list[index]);
            _settings.LiveOrder = list.ToList();
            Save();
            Show("即時狀態");
        }
    }

    // ── 模組 ────────────────────────────────────────────

    private UIElement BuildModules()
    {
        var p = new StackPanel();
        p.Children.Add(Caption("關掉的模組不會載入(也不會佔分頁)。重新啟動後套用。"));
        p.Children.Add(new Border { Height = 10 });
        foreach (var (id, title) in ModuleLoader.Discovered.OrderBy(d => OrderIndex(d.Id)))
        {
            bool enabled = !_settings.DisabledModules.Any(x => Same(x, id));
            p.Children.Add(ToggleCard(title, id, enabled, on =>
            {
                _settings.DisabledModules.RemoveAll(x => Same(x, id));
                if (!on) _settings.DisabledModules.Add(id);
                Save();
                NeedRestart();
            }));
        }
        return p;

        int OrderIndex(string id)
        {
            int i = _settings.Modules.FindIndex(x => Same(x, id));
            return i < 0 ? int.MaxValue : i;
        }
    }

    // ── 共用小工具 ─────────────────────────────────────

    private void Save() => _settings.Save();

    private void SaveAndApply()
    {
        _settings.Save();
        _main.ApplySettingsLive();
    }

    /// <summary>快捷鍵:點一下輸入框後直接按下想要的組合(至少一個 Ctrl / Alt / Shift / Win)。</summary>
    private Border HotkeyCard()
    {
        var box = new TextBox
        {
            Text = string.IsNullOrEmpty(_settings.Hotkey) ? "(未設定)" : _settings.Hotkey,
            IsReadOnly = true,
            Width = 170,
            TextAlignment = TextAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = (Style)FindResource(IslandTheme.SettingsTextBox),
        };
        var status = new TextBlock { Style = (Style)FindResource(IslandTheme.SettingsCaptionText) };
        box.GotKeyboardFocus += (_, _) => box.Text = "請按下組合鍵…";
        box.LostKeyboardFocus += (_, _) => box.Text = string.IsNullOrEmpty(_settings.Hotkey) ? "(未設定)" : _settings.Hotkey;
        box.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            var text = GlobalHotkey.Format(System.Windows.Input.Keyboard.Modifiers, key);
            if (text is null) return;   // 還在按修飾鍵
            var old = _settings.Hotkey;
            _settings.Hotkey = text;
            if (_main.ApplyHotkey())
            {
                Save();
                status.Text = $"已設定為 {text}。";
            }
            else
            {
                _settings.Hotkey = old;
                _main.ApplyHotkey();
                status.Text = $"{text} 已經被其他程式使用,換一組試試。";
            }
            box.Text = _settings.Hotkey;
            Keyboard.ClearFocus();
        };
        var clear = new Button { Content = "停用", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsButton) };
        clear.Click += (_, _) =>
        {
            _settings.Hotkey = "";
            _main.ApplyHotkey();
            Save();
            box.Text = "(未設定)";
            status.Text = "快捷鍵已停用。";
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(box);
        row.Children.Add(clear);

        var card = Card("快捷鍵", "叫出並固定展開島;展開時按 Esc 或再按一次收起。點輸入框後直接按下想要的組合。", row);
        ((Panel)((Grid)card.Child).Children[0]).Children.Add(status);
        return card;
    }

    private void NeedRestart() => RestartBar.Visibility = Visibility.Visible;

    private void OnRestartClick(object sender, RoutedEventArgs e) => ((App)Application.Current).Restart();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 16),
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xB8)),
        Margin = new Thickness(2, 14, 0, 8),
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
        TextWrapping = TextWrapping.Wrap,
    };

    private CheckBox Toggle(bool value, Action<bool> changed)
    {
        var box = new CheckBox { IsChecked = value, Style = (Style)FindResource(IslandTheme.SettingsToggle) };
        box.Checked += (_, _) => changed(true);
        box.Unchecked += (_, _) => changed(false);
        return box;
    }

    private Border ToggleCard(string title, string caption, bool value, Action<bool> changed) =>
        Card(title, caption, Toggle(value, changed));

    /// <summary>一張設定卡片:左邊標題 + 說明,右邊控制項(stacked = 控制項放在文字下方)。</summary>
    private Border Card(string title, string caption, UIElement control, bool stacked = false)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource(IslandTheme.SettingsTitleText) });
        if (!string.IsNullOrEmpty(caption))
            text.Children.Add(new TextBlock { Text = caption, Style = (Style)FindResource(IslandTheme.SettingsCaptionText) });

        FrameworkElement content;
        if (stacked)
        {
            var sp = new StackPanel();
            sp.Children.Add(text);
            sp.Children.Add(control);
            content = sp;
        }
        else
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(text);
            if (control is FrameworkElement fe) fe.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            content = grid;
        }
        return new Border { Style = (Style)FindResource(IslandTheme.SettingsCard), Child = content };
    }

    private Border SliderCard(string title, string caption, double min, double max, double step, double value, string unit, Action<double> changed)
    {
        var label = new TextBlock { Width = 52, TextAlignment = TextAlignment.Right, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = step,
            Value = Math.Clamp(value, min, max),
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("Settings.Slider"),
        };
        void UpdateLabel() => label.Text = $"{slider.Value:0}{unit}";
        UpdateLabel();
        slider.ValueChanged += (_, e) =>
        {
            UpdateLabel();
            if (Math.Abs(e.NewValue - e.OldValue) > 0.001) changed(Math.Round(e.NewValue));
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(slider);
        row.Children.Add(label);
        return Card(title, caption, row);
    }

    private Button SmallButton(string glyph, string tip, bool enabled, Action click)
    {
        var b = new Button
        {
            Content = glyph,
            ToolTip = tip,
            IsEnabled = enabled,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 11,
            Padding = new Thickness(9, 5, 9, 5),
            Margin = new Thickness(4, 0, 0, 0),
            Style = (Style)FindResource(IslandTheme.SettingsButton),
        };
        b.Click += (_, _) => click();
        return b;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

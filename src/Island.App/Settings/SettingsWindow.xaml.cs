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

    internal SettingsWindow(IslandSettings settings, MainWindow main, IReadOnlyList<IIslandModule> modules, string? startPage = null)
    {
        _settings = settings;
        _main = main;
        _modules = modules;
        InitializeComponent();

        _pages.Add(("歡迎", BuildWelcome));
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
        Navigate(startPage ?? "一般");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 深色標題列(Windows 11)
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    /// <summary>切到某一頁(左邊的選單也跟著選起來)。</summary>
    internal void Navigate(string title)
    {
        var item = Nav.Children.OfType<RadioButton>().FirstOrDefault(r => (string)r.Content == title)
                   ?? Nav.Children.OfType<RadioButton>().First(r => (string)r.Content == "一般");
        if (item.IsChecked == true) Show((string)item.Content);
        else item.IsChecked = true;
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
        p.Children.Add(ToggleCard("隱藏時顯示小瀏海", "島隱藏時,螢幕上緣中間留一小段黑色的瀏海,提醒滑鼠往哪裡推。",
            _settings.ShowNotch, on => { _settings.ShowNotch = on; Save(); _main.ApplySettingsLive(); }));
        p.Children.Add(TaskbarCard());
        p.Children.Add(ToggleCard("全螢幕時隱藏", "看影片、玩遊戲、簡報全螢幕時自動隱藏。",
            _settings.HideInFullscreen, on => { _settings.HideInFullscreen = on; Save(); _main.ApplySettingsLive(); }));
        p.Children.Add(ToggleCard("切歌時顯示", "換歌時膠囊出現約 3 秒,顯示封面和歌名。",
            _settings.AnnounceActivity, on => { _settings.AnnounceActivity = on; Save(); }));
        p.Children.Add(ToggleCard("音量亮度提示", "調音量或亮度時顯示長條。",
            _settings.ShowOsd, on => { _settings.ShowOsd = on; Save(); }));
        p.Children.Add(HotkeyCard());

        p.Children.Add(Section("備份與搬家"));
        var bundleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var exportAll = new Button { Content = "匯出設定…", Style = (Style)FindResource(IslandTheme.SettingsButton) };
        exportAll.Click += (_, _) => ExportSettings();
        var importAll = new Button { Content = "匯入設定…", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsButton) };
        importAll.Click += (_, _) => ImportSettings();
        bundleRow.Children.Add(exportAll);
        bundleRow.Children.Add(importAll);
        p.Children.Add(Card("匯出 / 匯入設定", "所有設定、主題、捷徑、待辦、各模組選項存成一個檔案,換電腦時匯入就好。不包含 Shelf 清單和私人日曆網址。匯入後 Island 會重新啟動。", bundleRow, stacked: false));
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
        foreach (var (id, title, defaultOn) in ModuleLoader.Discovered.OrderBy(d => OrderIndex(d.Id)))
        {
            bool enabled = ModuleLoader.IsEnabled(_settings, id, defaultOn);
            p.Children.Add(ToggleCard(title, defaultOn ? id : $"{id} · 預設關閉", enabled, on =>
            {
                ModuleLoader.SetEnabled(_settings, id, defaultOn, on);
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

    // ── 歡迎 ────────────────────────────────────────────

    private UIElement BuildWelcome()
    {
        var p = new StackPanel();
        p.Children.Add(Caption("Island 是放在螢幕上緣中間的「動態島」:平常完全隱藏,需要時才出現。三個步驟就能開始用。"));
        p.Children.Add(new Border { Height = 6 });

        p.Children.Add(Step("1", "叫出島", $"把滑鼠推到螢幕最上緣的正中間,島會滑下來;點一下固定展開,點別的地方收起。也可以按 {(_settings.Hotkey is { Length: > 0 } h ? h : "快捷鍵")}。",
            "試試看", () => _main.PreviewTheme()));
        p.Children.Add(Step("2", "選天氣的城市", "天氣、下雨提醒都看這個城市。", "設定天氣 →", () => Navigate("天氣")));
        p.Children.Add(Step("3", "挑一個主題", "顏色、圓角、字型都可以換,也能存成自己的主題分享給別人。", "挑主題 →", () => Navigate("主題")));

        p.Children.Add(Section("還可以"));
        p.Children.Add(Caption("· 把檔案、文字、網址、圖片拖到島上暫放(Shelf)\n· 設定 →「捷徑」加常用程式,標星號變成小圓,或按 Ctrl+Alt+1…9\n· 設定 →「模組」打開 行程提醒、剪貼簿紀錄、系統監看\n· 番茄鐘專注時,島只顯示倒數"));
        var done = new Button { Content = "開始使用", Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)FindResource(IslandTheme.SettingsAccentButton) };
        done.Click += (_, _) => Close();
        p.Children.Add(done);
        return p;
    }

    private Border Step(string number, string title, string caption, string action, Action click)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var badge = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        badge.SetResourceReference(Border.BackgroundProperty, IslandTheme.AccentDim);
        var num = new TextBlock { Text = number, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        num.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.AccentSoft);
        badge.Child = num;
        grid.Children.Add(badge);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource(IslandTheme.SettingsTitleText) });
        text.Children.Add(new TextBlock { Text = caption, Style = (Style)FindResource(IslandTheme.SettingsCaptionText) });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var button = new Button { Content = action, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Style = (Style)FindResource(IslandTheme.SettingsButton) };
        button.Click += (_, _) => click();
        Grid.SetColumn(button, 2);
        grid.Children.Add(button);
        return new Border { Style = (Style)FindResource(IslandTheme.SettingsCard), Child = grid };
    }

    // ── 匯出 / 匯入全部設定 ──

    private void ExportSettings()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "匯出 Island 設定",
            FileName = $"Island 設定 {DateTime.Now:yyyy-MM-dd}.json",
            Filter = "Island 設定 (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            _settings.Save();
            System.IO.File.WriteAllText(dlg.FileName, SettingsBundle.Export(AppPaths.DataDirectory));
        }
        catch (Exception ex) { IslandDialog.Show(this, $"匯出失敗:{ex.Message}"); }
    }

    private void ImportSettings()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "匯入 Island 設定", Filter = "Island 設定 (*.json)|*.json|所有檔案 (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            if (new System.IO.FileInfo(dlg.FileName).Length > 4 * 1024 * 1024) throw new System.IO.InvalidDataException("檔案太大,不是 Island 的設定檔");
            int n = SettingsBundle.Import(AppPaths.DataDirectory, System.IO.File.ReadAllText(dlg.FileName));
            Log.Write($"匯入設定:{n} 個檔案");
            if (IslandDialog.Confirm(this, $"已匯入 {n} 個設定檔,要現在重新啟動 Island 套用嗎?", "重新啟動", "稍後"))
                ((App)Application.Current).Restart();
        }
        catch (System.IO.InvalidDataException ex) { IslandDialog.Show(this, ex.Message); }
        catch (Exception ex) { IslandDialog.Show(this, $"匯入失敗:{ex.Message}"); }
    }

    // ── 共用小工具 ─────────────────────────────────────

    private void Save() => _settings.Save();

    private void SaveAndApply()
    {
        _settings.Save();
        _main.ApplySettingsLive();
    }

    /// <summary>快捷鍵:點一下輸入框後直接按下想要的組合(至少一個 Ctrl / Alt / Shift / Win)。</summary>
    private static bool SafeAutoHide()
    {
        try { return Taskbar.IsAutoHide; } catch { return false; }
    }

    /// <summary>
    /// 「工具列」卡片:上面是自動隱藏 Windows 工具列的開關,下面一排選「開始」按鈕放哪裡。
    /// </summary>
    private Border TaskbarCard()
    {
        var p = new StackPanel();

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = "自動隱藏 Windows 工具列", Style = (Style)FindResource(IslandTheme.SettingsTitleText) });
        text.Children.Add(new TextBlock { Text = "和 Windows 設定裡的「自動隱藏工作列」是同一個開關。打開後螢幕下方是空的,滑鼠碰到螢幕下緣工具列還是會出來。關掉 Island 不會自動改回來。", Style = (Style)FindResource(IslandTheme.SettingsCaptionText) });
        top.Children.Add(text);
        var toggle = Toggle(SafeAutoHide(), on =>
        {
            try { Taskbar.SetAutoHide(on); Log.Write($"工具列自動隱藏:{on}"); }
            catch (Exception ex) { Log.Write($"設定工具列自動隱藏失敗:{ex.Message}"); }
        });
        toggle.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(toggle, 1);
        top.Children.Add(toggle);
        p.Children.Add(top);

        p.Children.Add(new TextBlock { Text = "「開始」按鈕", Margin = new Thickness(0, 16, 0, 0), Style = (Style)FindResource(IslandTheme.SettingsTitleText) });
        p.Children.Add(new TextBlock { Text = "點一下打開 Windows 開始功能表(和按 Windows 鍵一樣)。", Style = (Style)FindResource(IslandTheme.SettingsCaptionText) });

        // 一排四個選項(像 iOS 的分段按鈕)
        var seg = new StackPanel { Orientation = Orientation.Horizontal };
        var box = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = seg,
        };
        var buttons = new List<(StartButtonPlace Place, Border Button)>();
        void Update()
        {
            foreach (var (place, b) in buttons)
                b.Background = place == _settings.StartButton ? new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)) : Brushes.Transparent;
        }
        foreach (var (place, label) in new[]
        {
            (StartButtonPlace.Row, "一排的左邊"),
            (StartButtonPlace.Strip, "小圓延伸列"),
            (StartButtonPlace.Tabs, "展開分頁列"),
            (StartButtonPlace.Off, "不顯示"),
        })
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 12.5 },
            };
            b.MouseLeftButtonUp += (_, _) =>
            {
                if (_settings.StartButton == place) return;
                _settings.StartButton = place;
                Save();
                _main.ApplySettingsLive();
                Update();
            };
            buttons.Add((place, b));
            seg.Children.Add(b);
        }
        Update();
        p.Children.Add(box);

        return new Border { Style = (Style)FindResource(IslandTheme.SettingsCard), Child = p };
    }

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
            if (control is FrameworkElement fe)
            {
                fe.Margin = new Thickness(16, 0, 0, 0);
                fe.VerticalAlignment = VerticalAlignment.Center;   // 說明文字很長時,按鈕不要被拉高
            }
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

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Links;

/// <summary>
/// 設定視窗的「捷徑」頁:目前的捷徑(排序、移除)、從開著的視窗加入、瀏覽程式 / 資料夾、輸入網址。
/// </summary>
internal sealed class LinksSettingsView : StackPanel
{
    private readonly LinksModule _module;
    private readonly StackPanel _current = new();
    private readonly StackPanel _windows = new();
    private readonly TextBlock _status;

    public LinksSettingsView(LinksModule module)
    {
        _module = module;

        Children.Add(Caption("點島上的捷徑:程式已經開著就切到它的視窗(最小化的會還原),沒開就幫你打開。也可以在「捷徑」分頁開著時,把程式、捷徑或資料夾直接拖到島上。\n標星號的捷徑會變成小圓:滑鼠移到島上就看得到,點一下直接切過去。其他捷徑展開島在「捷徑」分頁點。"));
        Children.Add(new Border { Height = 10 });

        // 目前的捷徑
        var currentCard = Card();
        var cp = new StackPanel();
        cp.Children.Add(Title("目前的捷徑"));
        cp.Children.Add(_current);
        currentCard.Child = cp;
        Children.Add(currentCard);

        // 從開著的視窗加入
        var winCard = Card();
        var wp = new StackPanel();
        var head = new DockPanel();
        var refresh = Button("重新整理", accent: false);
        refresh.Click += (_, _) => BuildWindows();
        DockPanel.SetDock(refresh, Dock.Right);
        head.Children.Add(refresh);
        head.Children.Add(Title("從開著的視窗加入"));
        wp.Children.Add(head);
        wp.Children.Add(Caption("下面是目前開著的程式和資料夾,按「加入」就會變成捷徑。"));
        wp.Children.Add(_windows);
        winCard.Child = wp;
        Children.Add(winCard);

        // 瀏覽 / 網址
        var moreCard = Card();
        var mp = new StackPanel();
        mp.Children.Add(Title("其他方式"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var browseApp = Button("選擇程式或檔案…", accent: false);
        browseApp.Click += (_, _) => BrowseFile();
        var browseFolder = Button("選擇資料夾…", accent: false);
        browseFolder.Margin = new Thickness(8, 0, 0, 0);
        browseFolder.Click += (_, _) => BrowseFolder();
        buttons.Children.Add(browseApp);
        buttons.Children.Add(browseFolder);
        mp.Children.Add(buttons);
        var urlRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var addUrl = Button("加入網址", accent: true);
        addUrl.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(addUrl, Dock.Right);
        urlRow.Children.Add(addUrl);
        var urlBox = new TextBox();
        urlBox.SetResourceReference(StyleProperty, IslandTheme.SettingsTextBox);
        urlRow.Children.Add(urlBox);
        void AddUrlNow()
        {
            var item = _module.AddUrl(urlBox.Text);
            Report(item, urlBox.Text);
            if (item is not null) urlBox.Text = "";
        }
        addUrl.Click += (_, _) => AddUrlNow();
        urlBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddUrlNow(); };
        mp.Children.Add(urlRow);
        _status = Caption("");
        _status.Margin = new Thickness(0, 8, 0, 0);
        mp.Children.Add(_status);
        moreCard.Child = mp;
        Children.Add(moreCard);

        module.Items.CollectionChanged += (_, _) => BuildCurrent();
        module.CircleChanged += (_, _) => BuildCurrent();
        BuildCurrent();
        BuildWindows();
    }

    private void BuildCurrent()
    {
        _current.Children.Clear();
        if (_module.Items.Count == 0)
        {
            var empty = Caption("還沒有捷徑。");
            empty.Margin = new Thickness(0, 6, 0, 0);
            _current.Children.Add(empty);
            return;
        }
        var circle = _module.CircleItem;
        for (int i = 0; i < _module.Items.Count; i++)
        {
            var item = _module.Items[i];
            bool isCircle = ReferenceEquals(item, circle);
            var row = Row(item.Icon, item.Name, isCircle ? $"小圓 · {item.Detail}" : item.Detail);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var star = IconButton(isCircle ? "" : "", isCircle ? "目前的小圓" : "設為小圓", true, () => _module.SetCircle(item));
            if (isCircle) star.SetResourceReference(Control.ForegroundProperty, IslandTheme.Accent);
            actions.Children.Add(star);
            actions.Children.Add(IconButton("\uE70E", "往前", i > 0, () => _module.Move(item, -1)));
            actions.Children.Add(IconButton("\uE70D", "往後", i < _module.Items.Count - 1, () => _module.Move(item, +1)));
            actions.Children.Add(IconButton("\uE711", "移除", true, () => _module.Remove(item)));
            DockPanel.SetDock(actions, Dock.Right);
            row.Children.Insert(0, actions);
            _current.Children.Add(row);
        }
    }

    private void BuildWindows()
    {
        _windows.Children.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in WindowFinder.List())
        {
            var key = w.FolderPath ?? w.ExePath;
            if (!seen.Add(key)) continue;   // 同一個程式開了好幾個視窗,只列一次
            var name = w.FolderPath is not null ? Path.GetFileName(w.FolderPath.TrimEnd('\\')) : w.Title;
            if (string.IsNullOrEmpty(name)) name = w.FolderPath ?? w.Title;
            var detail = w.FolderPath is not null ? $"資料夾 · {w.FolderPath}" : Path.GetFileName(w.ExePath);
            var row = Row(LinkIcons.Get(w.FolderPath ?? w.ExePath), name, detail);
            var add = Button("加入", accent: false);
            add.Padding = new Thickness(12, 4, 12, 4);
            var window = w;
            add.Click += (_, _) => Report(_module.AddWindow(window), name);
            DockPanel.SetDock(add, Dock.Right);
            row.Children.Insert(0, add);
            _windows.Children.Add(row);
        }
        if (_windows.Children.Count == 0) _windows.Children.Add(Caption("目前沒有開著的視窗。"));
    }

    private void BrowseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "選擇程式、捷徑或檔案",
            Filter = "程式與捷徑 (*.exe;*.lnk)|*.exe;*.lnk|所有檔案 (*.*)|*.*",
            DereferenceLinks = false,
        };
        if (dlg.ShowDialog() == true) Report(_module.AddPath(dlg.FileName), Path.GetFileName(dlg.FileName));
    }

    private void BrowseFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "選擇資料夾" };
        if (dlg.ShowDialog() == true) Report(_module.AddPath(dlg.FolderName), Path.GetFileName(dlg.FolderName));
    }

    private void Report(LinkItem? item, string what) =>
        _status.Text = item is not null ? $"已加入「{item.Name}」。" : $"「{what}」沒有加入(可能已經在清單裡,或格式不對)。";

    // ── 小工具 ──

    private static DockPanel Row(ImageSource? icon, string name, string detail)
    {
        var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = true };
        var img = new Image { Source = icon, Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        DockPanel.SetDock(img, Dock.Left);
        row.Children.Add(img);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = name, FontSize = 12.5, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = detail, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)), TextTrimming = TextTrimming.CharacterEllipsis });
        row.Children.Add(text);
        return row;
    }

    private static Button Button(string text, bool accent)
    {
        var b = new Button { Content = text };
        b.SetResourceReference(StyleProperty, accent ? IslandTheme.SettingsAccentButton : IslandTheme.SettingsButton);
        return b;
    }

    private static Button IconButton(string glyph, string tip, bool enabled, Action click)
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
        };
        b.SetResourceReference(StyleProperty, IslandTheme.SettingsButton);
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Card()
    {
        var b = new Border();
        b.SetResourceReference(StyleProperty, IslandTheme.SettingsCard);
        return b;
    }

    private static TextBlock Title(string text)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, IslandTheme.SettingsTitleText);
        return t;
    }

    private static TextBlock Caption(string text)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, IslandTheme.SettingsCaptionText);
        return t;
    }
}

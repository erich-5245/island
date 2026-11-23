using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Todo;

/// <summary>
/// 待辦:展開「待辦」分頁勾選 / 刪除 / 新增;還有沒做完的事時,滑鼠移過去那一排右邊顯示「待辦 N」。
/// 島本身不能打字(不搶焦點),所以「新增」會跳出一個小輸入框。
/// 清單存在 %LOCALAPPDATA%\Island\modules\todo\todos.json。平常不耗資源(沒有計時器)。
/// </summary>
public sealed class TodoModule : IslandModuleBase, IIslandCompact
{
    private readonly TodoList _list = new();
    private string _file = "";
    private TodoView? _view;
    private TextBlock? _circleCount;

    public override string Id => "todo";
    public override string Title => "待辦";

    internal TodoList List => _list;

    protected override void OnInitialize()
    {
        _file = Path.Combine(Host.GetModuleDataDirectory(Id), "todos.json");
        try { if (File.Exists(_file)) _list.LoadJson(File.ReadAllText(_file)); }
        catch (Exception ex) { Host.Log($"待辦:讀取失敗:{ex.Message}"); }
        UpdateBadge();
    }

    public override FrameworkElement CreateExpandedView() => _view ??= new TodoView(this);

    // ── 小圓:未完成數量;點了跳出新增框 ──
    public bool IsCompactRelevant => _list.Pending > 0;

    public FrameworkElement CreateCompactView()
    {
        var g = new Grid { Width = 30, Height = 30 };
        g.Children.Add(new TextBlock
        {
            Text = "",   // 勾選框
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2),
        });
        var badge = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(3, 0, 3, 0), MinWidth = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        badge.SetResourceReference(Border.BackgroundProperty, IslandTheme.Accent);
        _circleCount = new TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
        _circleCount.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.OnAccent);
        badge.Child = _circleCount;
        g.Children.Add(badge);
        UpdateBadge();
        return g;
    }

    public string? CompactActionLabel => "新增待辦";
    public void RunCompactAction(int index) => PromptAdd();

    // ── 操作 ──

    public void PromptAdd()
    {
        var text = TodoInputWindow.Ask();
        if (text is null) return;
        if (_list.Add(text) is not null) Changed();
    }

    public void Toggle(TodoItem item) { _list.Toggle(item); Changed(); }
    public void Remove(TodoItem item) { if (_list.Remove(item)) Changed(); }
    public void ClearDone() { if (_list.ClearDone() > 0) Changed(); }

    private void Changed()
    {
        try { File.WriteAllText(_file, _list.ToJson()); }
        catch (Exception ex) { Host.Log($"待辦:寫入失敗:{ex.Message}"); }
        UpdateBadge();
        _view?.Refresh();
    }

    private void UpdateBadge()
    {
        int n = _list.Pending;
        if (_circleCount is not null) _circleCount.Text = n > 99 ? "99+" : n.ToString();
        SetBadge(n == 0 ? null : new IslandBadge($"待辦 {n}"));
    }
}

/// <summary>「待辦」分頁:清單(點文字勾選、× 刪除)+ 新增 / 清除已完成。</summary>
internal sealed class TodoView : Grid
{
    private readonly TodoModule _module;
    private readonly StackPanel _items = new();
    private readonly TextBlock _empty;
    private readonly Button _clear;

    public TodoView(TodoModule module)
    {
        _module = module;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = false };
        var add = TextButton("＋ 新增");
        add.Click += (_, _) => _module.PromptAdd();
        _clear = TextButton("清除已完成");
        _clear.Click += (_, _) => _module.ClearDone();
        DockPanel.SetDock(add, Dock.Left);
        DockPanel.SetDock(_clear, Dock.Right);
        header.Children.Add(add);
        header.Children.Add(_clear);
        Children.Add(header);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = _items, Focusable = false };
        SetRow(scroll, 1);
        Children.Add(scroll);

        _empty = new TextBlock
        {
            Text = "沒有待辦事項,按「＋ 新增」加一件",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SetRow(_empty, 1);
        Children.Add(_empty);
        Refresh();
    }

    public void Refresh()
    {
        _items.Children.Clear();
        foreach (var item in _module.List.Items) _items.Children.Add(Row(item));
        _empty.Visibility = _module.List.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _clear.Visibility = _module.List.Items.Any(i => i.Done) ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement Row(TodoItem item)
    {
        var row = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(6, 3, 4, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var dock = new DockPanel();
        var check = new TextBlock
        {
            Text = item.Done ? "" : "",   // 已勾 / 空框
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        if (item.Done) check.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        else check.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
        DockPanel.SetDock(check, Dock.Left);
        dock.Children.Add(check);

        var del = new Button
        {
            Content = "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 9,
            Visibility = Visibility.Hidden,
            ToolTip = "刪除",
            Focusable = false,
            Template = PlainTemplate(),
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            Padding = new Thickness(6, 2, 6, 2),
        };
        del.Click += (_, e) => { e.Handled = true; _module.Remove(item); };
        DockPanel.SetDock(del, Dock.Right);
        dock.Children.Add(del);

        var text = new TextBlock
        {
            Text = item.Text,
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = item.Done ? new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)) : Brushes.White,
            TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
            ToolTip = item.Text.Length > 30 ? item.Text : null,
        };
        dock.Children.Add(text);
        row.Child = dock;
        row.MouseEnter += (_, _) => { row.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)); del.Visibility = Visibility.Visible; };
        row.MouseLeave += (_, _) => { row.Background = Brushes.Transparent; del.Visibility = Visibility.Hidden; };
        row.MouseLeftButtonUp += (_, _) => _module.Toggle(item);
        return row;
    }

    private static Button TextButton(string text)
    {
        var b = new Button
        {
            Content = text,
            FontSize = 11.5,
            Focusable = false,
            Cursor = Cursors.Hand,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
            Padding = new Thickness(8, 2, 8, 2),
            Template = PlainTemplate(),
        };
        return b;
    }

    /// <summary>透明按鈕,滑鼠移過去有淡淡的底色、文字變白。</summary>
    private static ControlTemplate PlainTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var bg = new FrameworkElementFactory(typeof(Border), "Bg");
        bg.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bg.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bg.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        bg.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = bg;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), "Bg"));
        hover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        t.Triggers.Add(hover);
        return t;
    }
}

/// <summary>新增待辦的小輸入框(深色、置頂、Enter 新增、Esc 或點別的地方取消)。</summary>
internal sealed class TodoInputWindow : Window
{
    private readonly TextBox _box;
    private string? _result;
    private bool _closing;

    private TodoInputWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.Height;
        Width = 360;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + 70;

        _box = new TextBox
        {
            FontSize = 14,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxLength = TodoList.MaxLength,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var hint = new TextBlock { Text = "新增待辦,按 Enter", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
        _box.TextChanged += (_, _) => hint.Visibility = _box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var grid = new Grid { Children = { hint, _box } };
        var frame = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10, 14, 10),
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
        Content = frame;

        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { _result = _box.Text; Close(); }
            else if (e.Key == Key.Escape) Close();
        };
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => { if (IsLoaded && !_closing) Close(); };
        Loaded += (_, _) => { Activate(); _box.Focus(); Keyboard.Focus(_box); };
    }

    /// <summary>顯示輸入框,回傳輸入的文字;取消回傳 null。</summary>
    public static string? Ask()
    {
        var w = new TodoInputWindow();
        w.ShowDialog();
        return string.IsNullOrWhiteSpace(w._result) ? null : w._result;
    }
}

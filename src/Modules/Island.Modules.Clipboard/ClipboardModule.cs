using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Clipboard;

/// <summary>剪貼簿紀錄的純邏輯:最新的在最上面、重複的移到最上面、最多 N 筆。</summary>
public sealed class ClipHistory
{
    public const int MaxEntries = 20;
    public const int MaxChars = 100_000;

    private readonly List<string> _items = [];
    public IReadOnlyList<string> Items => _items;

    /// <summary>加入一筆;空白或和最上面那筆一樣時回傳 false。</summary>
    public bool Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length > MaxChars) text = text[..MaxChars];
        if (_items.Count > 0 && _items[0] == text) return false;
        _items.Remove(text);
        _items.Insert(0, text);
        if (_items.Count > MaxEntries) _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
        return true;
    }

    public bool Remove(string text) => _items.Remove(text);
    public void Clear() => _items.Clear();

    /// <summary>清單上顯示的一行:第一個非空白行,空白合併,太長截斷。</summary>
    public static string Preview(string text, int max = 40)
    {
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        line = string.Join(" ", line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        bool more = line.Length > max || text.Trim().Contains('\n');
        if (line.Length > max) line = line[..(max - 1)];
        return more ? line + "…" : line;
    }
}

/// <summary>
/// 剪貼簿紀錄:記住最近複製的 20 筆文字,點一下重新複製、或丟進 Shelf。預設關閉(設定 →「模組」打開)。
/// 隱私:只存在記憶體(關掉 Island 就消失,不寫入檔案);密碼管理器標記「不要記錄」的內容會略過。
/// 效能:用 AddClipboardFormatListener 等 Windows 通知,沒有輪詢。
/// </summary>
public sealed class ClipboardModule : IslandModuleBase
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly string[] SkipFormats =
    [
        "ExcludeClipboardContentFromMonitorProcessing",   // 密碼管理器等
        "Clipboard Viewer Ignore",
    ];

    private readonly ClipHistory _history = new();
    private HwndSource? _listener;
    private ClipView? _view;
    private string? _ownCopy;   // 自己剛複製回去的內容,不要再記一次

    public override string Id => "clipboard";
    public override string Title => "剪貼簿";
    public override bool EnabledByDefault => false;

    internal ClipHistory History => _history;

    protected override void OnInitialize()
    {
        // 只收訊息的隱藏視窗(HWND_MESSAGE),不會出現在畫面或工作列
        var p = new HwndSourceParameters("IslandClipboardListener") { ParentWindow = new IntPtr(-3), WindowStyle = 0 };
        _listener = new HwndSource(p);
        _listener.AddHook(WndProc);
        if (!AddClipboardFormatListener(_listener.Handle)) Host.Log("剪貼簿:無法監聽剪貼簿");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) Host.Dispatcher.BeginInvoke(Capture);
        return IntPtr.Zero;
    }

    private void Capture()
    {
        string? text = null;
        for (int attempt = 0; attempt < 3 && text is null; attempt++)
        {
            try
            {
                var data = System.Windows.Clipboard.GetDataObject();
                if (data is null) return;
                if (SkipFormats.Any(data.GetDataPresent)) return;
                if (!data.GetDataPresent(DataFormats.UnicodeText)) return;
                text = data.GetData(DataFormats.UnicodeText) as string ?? "";
            }
            catch (COMException)
            {
                Thread.Sleep(30);   // 別的程式正在用剪貼簿,稍等再試
            }
        }
        if (text is null) return;
        if (text == _ownCopy) { _ownCopy = null; return; }
        if (_history.Add(text)) _view?.Refresh();
    }

    public override FrameworkElement CreateExpandedView() => _view ??= new ClipView(this);

    public void CopyBack(string text)
    {
        try
        {
            _ownCopy = text;
            System.Windows.Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), copy: true);
            _history.Add(text);   // 移到最上面
            _view?.Refresh();
        }
        catch (Exception ex) { Host.Log($"剪貼簿:複製失敗:{ex.Message}"); }
    }

    public void ToShelf(string text)
    {
        if (!Host.SendToModule("shelf", new DataObject(DataFormats.UnicodeText, text))) Host.Log("剪貼簿:Shelf 沒有開啟");
    }

    public void Remove(string text) { if (_history.Remove(text)) _view?.Refresh(); }
    public void Clear() { _history.Clear(); _view?.Refresh(); }

    public override void Dispose()
    {
        if (_listener is not null)
        {
            RemoveClipboardFormatListener(_listener.Handle);
            _listener.Dispose();
        }
        base.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}

/// <summary>「剪貼簿」分頁:點一筆 = 重新複製;滑鼠移過去出現「放進 Shelf」與刪除。</summary>
internal sealed class ClipView : Grid
{
    private readonly ClipboardModule _module;
    private readonly StackPanel _items = new();
    private readonly TextBlock _empty;
    private readonly Button _clear;
    private string? _flash;

    public ClipView(ClipboardModule module)
    {
        _module = module;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = false };
        header.Children.Add(new TextBlock { Text = "點一下重新複製 · 只存在記憶體", FontSize = 11, Foreground = Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        _clear = TextButton("全部清除", 11.5);
        _clear.Click += (_, _) => _module.Clear();
        DockPanel.SetDock(_clear, Dock.Right);
        header.Children.Add(_clear);
        Children.Add(header);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = _items, Focusable = false };
        SetRow(scroll, 1);
        Children.Add(scroll);
        _empty = new TextBlock { Text = "複製一些文字,這裡就會出現", FontSize = 12, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        SetRow(_empty, 1);
        Children.Add(_empty);
        Refresh();
    }

    private static readonly SolidColorBrush Dim = new(Color.FromRgb(0x9A, 0x9A, 0x9A));

    public void Refresh()
    {
        _items.Children.Clear();
        foreach (var text in _module.History.Items) _items.Children.Add(Row(text));
        bool any = _module.History.Items.Count > 0;
        _empty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        _clear.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement Row(string text)
    {
        var row = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(6, 3, 4, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var dock = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Hidden };
        var shelf = TextButton("", 10, icon: true, tip: "放進 Shelf");
        shelf.Click += (_, e) => { e.Handled = true; _module.ToShelf(text); };
        var del = TextButton("", 9, icon: true, tip: "刪除");
        del.Click += (_, e) => { e.Handled = true; _module.Remove(text); };
        actions.Children.Add(shelf);
        actions.Children.Add(del);
        DockPanel.SetDock(actions, Dock.Right);
        dock.Children.Add(actions);
        var label = new TextBlock
        {
            Text = _flash == text ? "✓ 已複製" : ClipHistory.Preview(text),
            FontSize = 12.5,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = text.Length > 40 ? (text.Length > 500 ? text[..500] + "…" : text) : null,
        };
        dock.Children.Add(label);
        row.Child = dock;
        row.MouseEnter += (_, _) => { row.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)); actions.Visibility = Visibility.Visible; };
        row.MouseLeave += (_, _) => { row.Background = Brushes.Transparent; actions.Visibility = Visibility.Hidden; };
        row.MouseLeftButtonUp += (_, _) =>
        {
            _flash = text;
            _module.CopyBack(text);   // 會重畫清單,這一列暫時顯示「✓ 已複製」
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            t.Tick += (_, _) => { t.Stop(); _flash = null; Refresh(); };
            t.Start();
        };
        return row;
    }

    private static Button TextButton(string text, double size, bool icon = false, string? tip = null)
    {
        var t = new ControlTemplate(typeof(Button));
        var bg = new FrameworkElementFactory(typeof(Border), "Bg");
        bg.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bg.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bg.SetValue(Border.PaddingProperty, new Thickness(7, 2, 7, 2));
        bg.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = bg;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)), "Bg"));
        hover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        t.Triggers.Add(hover);
        var b = new Button { Content = text, FontSize = size, Focusable = false, Cursor = Cursors.Hand, Foreground = Dim, Template = t, ToolTip = tip };
        if (icon) b.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        return b;
    }
}

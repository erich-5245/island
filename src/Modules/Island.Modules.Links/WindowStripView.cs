using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Island.Modules.Links;

/// <summary>
/// 捷徑小圓往外延伸的那一條:[小圓的捷徑] | [開著的視窗…] [+N]。
/// - 只在延伸時存在;開著的期間用 WinEvent 監看視窗開 / 關 / 換前景,有變化才重排(平常完全不耗資源)。
/// - 點一格:切到那個程式(已經在前面就輪到它的下一個視窗);中鍵:關閉那個視窗。
/// </summary>
internal sealed class WindowStripView : StackPanel
{
    private const double Cell = 30, Tile = 24;

    private readonly LinksModule _module;
    private readonly LinkItem? _pinned;
    private readonly bool _towardLeft;
    private readonly double _height;
    private readonly DispatcherTimer _debounce;
    private readonly WinEventProc _proc;   // 要留著,不然會被回收
    private readonly List<IntPtr> _hooks = [];
    private string _signature = "";

    private static readonly Dictionary<string, (ImageSource? Icon, Color Tint)> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public WindowStripView(LinksModule module, LinkItem? pinned, bool towardLeft, double height)
    {
        _module = module;
        _pinned = pinned;
        _towardLeft = towardLeft;
        _height = height;
        Orientation = Orientation.Horizontal;
        Height = height;
        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Rebuild(); };
        _proc = OnWinEvent;
        Rebuild();
        // 視窗換前景 / 最小化還原,以及視窗出現 / 消失
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
        _hooks.Add(SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
    }

    /// <summary>收起:停止監看。</summary>
    public void Stop()
    {
        _debounce.Stop();
        foreach (var h in _hooks) if (h != IntPtr.Zero) UnhookWinEvent(h);
        _hooks.Clear();
    }

    private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero) return;   // 只要「視窗本身」的事件
        _debounce.Stop();
        _debounce.Start();
    }

    // ── 內容 ───────────────────────────────────────────

    private void Rebuild()
    {
        var foreground = WindowFinder.Foreground();
        var pinnedExe = _pinned?.Kind is LinkKind.App or LinkKind.Shortcut ? _pinned.Exe : null;
        var (shown, overflow) = WindowGroups.Build(WindowFinder.List(includeFolders: false), pinnedExe, foreground);

        // 沒變就不重畫(換前景、標題列以外的事件很多)
        var sig = string.Join("|", shown.Select(g => $"{g.Key}:{g.Windows.Count}:{g.IsActive}")) + "+" + overflow.Count;
        if (sig == _signature) return;
        _signature = sig;

        var cells = new List<UIElement> { PinnedCell(), Separator() };
        cells.AddRange(shown.Select(GroupCell));
        if (overflow.Count > 0) cells.Add(MoreCell(overflow));
        if (shown.Count == 0 && overflow.Count == 0) cells.Add(new TextBlock
        {
            Text = "沒有其他開著的視窗",
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 6, 0),
        });
        if (_towardLeft) cells.Reverse();

        Children.Clear();
        // 第一格(小圓本身的圖示)要剛好在原本小圓的正中間
        double edge = (_height - Cell) / 2;
        Margin = _towardLeft ? new Thickness(6, 0, edge, 0) : new Thickness(edge, 0, 6, 0);
        foreach (var c in cells) Children.Add(c);
    }

    /// <summary>小圓本身的捷徑:不處理點擊,讓外殼當成「點小圓」(維持原本的動作)。</summary>
    private UIElement PinnedCell()
    {
        var cell = new Grid { Width = Cell, Height = _height, Background = Brushes.Transparent, ToolTip = _pinned is null ? null : $"切到 {_pinned.Name}" };
        if (_pinned?.Icon is { } icon) cell.Children.Add(IconImage(icon, 18));
        return cell;
    }

    private UIElement Separator() => new Border
    {
        Width = 1,
        Height = _height * 0.5,
        Margin = new Thickness(4, 0, 4, 0),
        Background = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private UIElement GroupCell(WindowGroup group)
    {
        var (icon, tint) = IconFor(group.First);
        var cell = new Grid { Width = Cell, Height = _height, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var tile = new Border
        {
            Width = Tile,
            Height = Tile,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromArgb(0x55, tint.R, tint.G, tint.B)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3),
            Child = icon is null
                ? new TextBlock { Text = group.First.Title[..1], Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : IconImage(icon, 16),
        };
        cell.Children.Add(tile);

        // 底下的指示:白色長條 = 現在在前面,灰點 = 背景
        cell.Children.Add(new Border
        {
            Width = group.IsActive ? 12 : 4,
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = group.IsActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, Math.Max(1, (_height - Tile) / 2 - 4)),
        });

        // 同一個程式好幾個視窗:右上角小數字
        if (group.Windows.Count > 1)
        {
            cell.Children.Add(new Border
            {
                MinWidth = 13,
                Height = 13,
                CornerRadius = new CornerRadius(6.5),
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, (_height - Tile) / 2 - 4, -1, 0),
                Padding = new Thickness(3, 0, 3, 0),
                Child = new TextBlock { Text = group.Windows.Count.ToString(), FontSize = 9, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            });
        }

        cell.ToolTip = group.Windows.Count == 1 ? group.First.Title : $"{group.First.Title}(共 {group.Windows.Count} 個視窗,連點輪流切換)";
        cell.MouseEnter += (_, _) => tile.RenderTransform = new ScaleTransform(1.1, 1.1, Tile / 2, Tile / 2);
        cell.MouseLeave += (_, _) => tile.RenderTransform = null;
        cell.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _module.SwitchTo(WindowGroups.Next(group, WindowFinder.Foreground()));
        };
        cell.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            WindowFinder.Close(group.First.Handle);   // 關掉這個程式最近用的那個視窗;其他的還在
        };
        return cell;
    }

    /// <summary>放不下的程式:「+N」,點了列出來。</summary>
    private UIElement MoreCell(List<WindowGroup> overflow)
    {
        var text = new TextBlock
        {
            Text = $"+{overflow.Count}",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var cell = new Grid { MinWidth = Cell, Height = _height, Background = Brushes.Transparent, Cursor = Cursors.Hand, Children = { text }, ToolTip = "其他開著的視窗" };
        cell.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var menu = new ContextMenu { PlacementTarget = cell, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var w in overflow.SelectMany(g => g.Windows))
            {
                var (icon, _) = IconFor(w);
                var item = new MenuItem { Header = w.Title.Length > 60 ? w.Title[..60] + "…" : w.Title, Icon = icon is null ? null : IconImage(icon, 16) };
                item.Click += (_, _) => _module.SwitchTo(w);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        };
        return cell;
    }

    private static Image IconImage(ImageSource icon, double size)
    {
        var img = new Image { Source = icon, Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    // ── 圖示與主題色(每個程式只算一次) ──

    private static (ImageSource? Icon, Color Tint) IconFor(AppWindow w)
    {
        var key = WindowGroups.KeyOf(w);
        if (IconCache.TryGetValue(key, out var hit)) return hit;
        var icon = key.StartsWith("hwnd:", StringComparison.Ordinal)
            ? WindowFinder.WindowIcon(w.Handle)
            : LinkIcons.Get(w.ExePath) ?? WindowFinder.WindowIcon(w.Handle);
        var result = (icon, icon is BitmapSource b ? Dominant(b) : Color.FromRgb(0x60, 0x60, 0x60));
        if (!key.StartsWith("hwnd:", StringComparison.Ordinal)) IconCache[key] = result;
        return result;
    }

    /// <summary>圖示的主色:不透明、比較鮮豔的像素加權平均;灰階圖示就用中性灰。</summary>
    internal static Color Dominant(BitmapSource source)
    {
        try
        {
            var bmp = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var px = new byte[w * h * 4];
            bmp.CopyPixels(px, w * 4, 0);
            return Dominant(px);
        }
        catch { return Color.FromRgb(0x60, 0x60, 0x60); }
    }

    internal static Color Dominant(byte[] bgra)
    {
        double r = 0, g = 0, b = 0, weight = 0;
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128) continue;
            int max = Math.Max(bgra[i], Math.Max(bgra[i + 1], bgra[i + 2]));
            int min = Math.Min(bgra[i], Math.Min(bgra[i + 1], bgra[i + 2]));
            double sat = max == 0 ? 0 : (max - min) / (double)max;
            double wgt = 0.05 + sat * sat;   // 鮮豔的像素比較重要
            b += bgra[i] * wgt; g += bgra[i + 1] * wgt; r += bgra[i + 2] * wgt;
            weight += wgt;
        }
        if (weight == 0) return Color.FromRgb(0x60, 0x60, 0x60);
        return Color.FromRgb((byte)(r / weight), (byte)(g / weight), (byte)(b / weight));
    }

    // ── Win32 ──

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    private const uint EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_HIDE = 0x8003;   // DESTROY、SHOW、HIDE
    private const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;

    private delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}

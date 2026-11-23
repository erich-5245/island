using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Island.Core;

/// <summary>
/// 和設定視窗同一套深色風格的選色視窗(取代 Windows 內建那個舊式「色彩」對話方塊)。
/// 上面是飽和度 / 亮度方塊,下面是色相條、常用色、色碼輸入,以及「原本 → 新的」對照。
/// 外殼和模組共用:<c>IslandColorPicker.Pick(owner, 目前顏色)</c>,取消回傳 null。
/// </summary>
public static class IslandColorPicker
{
    /// <summary>這次執行期間選過的顏色(最新的在前),下次打開時放在常用色後面。</summary>
    private static readonly List<Color> Recent = [];

    public static Color? Pick(Window? owner, Color initial)
    {
        var window = new ColorPickerWindow(initial) { Owner = owner };
        if (owner is null) window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (window.ShowDialog() != true) return null;
        var chosen = window.Selected;
        Recent.RemoveAll(c => c == chosen);
        Recent.Insert(0, chosen);
        if (Recent.Count > 8) Recent.RemoveRange(8, Recent.Count - 8);
        return chosen;
    }

    // ── HSV ↔ RGB(純計算,方便測試) ──

    /// <summary>h 0–360、s 0–1、v 0–1。</summary>
    public static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>接受 "#RRGGBB"、"RRGGBB"、"#RGB"。</summary>
    public static bool TryParseHex(string? text, out Color color)
    {
        color = Colors.White;
        var t = (text ?? "").Trim().TrimStart('#');
        if (t.Length == 3) t = string.Concat(t.Select(ch => $"{ch}{ch}"));
        if (t.Length != 6 || !uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    private sealed class ColorPickerWindow : Window
    {
        private const double SvWidth = 300, SvHeight = 170, HueHeight = 14;
        private static readonly string[] Swatches = ["#FFFFFF", "#8E8E93", "#FF453A", "#FF9F0A", "#FFD60A", "#30D158", "#64D2FF", "#0A84FF", "#BF5AF2", "#FF6482"];

        private readonly Color _initial;
        private double _h, _s, _v;
        private bool _updating;

        private readonly Border _svBox;
        private readonly Ellipse _svThumb;
        private readonly Border _hueThumb;
        private readonly Border _newSwatch;
        private readonly TextBox _hex;

        public Color Selected => FromHsv(_h, _s, _v);

        public ColorPickerWindow(Color initial)
        {
            _initial = initial;
            (_h, _s, _v) = ToHsv(initial);

            Title = "選擇顏色";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft JhengHei UI, Segoe UI");

            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };

            // 標題列(可拖動)
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 14), Background = Brushes.Transparent };
            var close = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6),
            };
            close.MouseLeftButtonUp += (_, _) => DialogResult = false;
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = "選擇顏色", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            head.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            root.Children.Add(head);

            // 飽和度(左→右)/ 亮度(上→下)方塊
            var white = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0);
            var black = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90);
            white.Freeze();
            black.Freeze();
            _svBox = new Border { Width = SvWidth, Height = SvHeight, CornerRadius = new CornerRadius(10), Cursor = Cursors.Cross };
            _svThumb = new Ellipse { Width = 16, Height = 16, Stroke = Brushes.White, StrokeThickness = 2.5, IsHitTestVisible = false, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Opacity = 0.6 } };
            var svCanvas = new Canvas { Width = SvWidth, Height = SvHeight, ClipToBounds = false };
            svCanvas.Children.Add(_svThumb);
            _svBox.Child = new Grid
            {
                Children =
                {
                    new Border { CornerRadius = new CornerRadius(10), Background = white },
                    new Border { CornerRadius = new CornerRadius(10), Background = black },
                    svCanvas,
                },
            };
            Drag(_svBox, p =>
            {
                _s = Math.Clamp(p.X / SvWidth, 0, 1);
                _v = 1 - Math.Clamp(p.Y / SvHeight, 0, 1);
                Refresh();
            });
            root.Children.Add(_svBox);

            // 色相條
            var hue = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int i = 0; i <= 6; i++) hue.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
            hue.Freeze();
            _hueThumb = new Border { Width = 8, Height = HueHeight + 6, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(2), IsHitTestVisible = false };
            var hueCanvas = new Canvas { Width = SvWidth, Height = HueHeight };
            Canvas.SetTop(_hueThumb, -3);
            hueCanvas.Children.Add(_hueThumb);
            var hueBar = new Border
            {
                Width = SvWidth,
                Height = HueHeight,
                CornerRadius = new CornerRadius(HueHeight / 2),
                Background = hue,
                Margin = new Thickness(0, 14, 0, 0),
                Cursor = Cursors.Hand,
                Child = hueCanvas,
            };
            Drag(hueBar, p =>
            {
                _h = Math.Clamp(p.X / SvWidth, 0, 0.9999) * 360;
                Refresh();
            });
            root.Children.Add(hueBar);

            // 常用色 + 這次選過的
            var swatches = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), Width = SvWidth + 6 };
            foreach (var hex in Swatches.Concat(Recent.Select(ToHex)).Distinct().Take(18))
            {
                TryParseHex(hex, out var c);
                var dot = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(c),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand,
                    ToolTip = hex,
                };
                dot.MouseLeftButtonUp += (_, _) => SetColor(c);
                swatches.Children.Add(dot);
            }
            root.Children.Add(swatches);

            // 原本 → 新的、色碼
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = false };
            var oldSwatch = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(10, 0, 0, 10), Background = new SolidColorBrush(initial), Cursor = Cursors.Hand, ToolTip = "回到原本的顏色" };
            oldSwatch.MouseLeftButtonUp += (_, _) => SetColor(_initial);
            _newSwatch = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(0, 10, 10, 0) };
            var pair = new Border
            {
                CornerRadius = new CornerRadius(11),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { oldSwatch, _newSwatch } },
            };
            row.Children.Add(pair);
            _hex = new TextBox { Width = 110, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MaxLength = 7 };
            if (Application.Current?.TryFindResource(IslandTheme.SettingsTextBox) is Style tb) _hex.Style = tb;
            _hex.TextChanged += (_, _) =>
            {
                if (_updating || !TryParseHex(_hex.Text, out var c) || _hex.Text.Trim().TrimStart('#').Length != 6) return;
                (_h, _s, _v) = ToHsv(c);
                Refresh(fromHex: true);
            };
            row.Children.Add(_hex);
            root.Children.Add(row);

            // 按鈕
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            var cancel = new Button { Content = "取消", IsCancel = true, MinWidth = 76 };
            var ok = new Button { Content = "確定", IsDefault = true, MinWidth = 76, Margin = new Thickness(8, 0, 0, 0) };
            if (Application.Current?.TryFindResource(IslandTheme.SettingsButton) is Style b1) cancel.Style = b1;
            if (Application.Current?.TryFindResource(IslandTheme.SettingsAccentButton) is Style b2) ok.Style = b2;
            cancel.Click += (_, _) => DialogResult = false;
            ok.Click += (_, _) => DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(12),   // 留給陰影
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.5 },
                Child = root,
            };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
            Refresh();
        }

        private void SetColor(Color c)
        {
            (_h, _s, _v) = ToHsv(c);
            Refresh();
        }

        /// <summary>拖動(按下、移動都算),座標限制在元素裡。</summary>
        private static void Drag(FrameworkElement element, Action<Point> moved)
        {
            element.MouseLeftButtonDown += (_, e) =>
            {
                element.CaptureMouse();
                moved(e.GetPosition(element));
                e.Handled = true;
            };
            element.MouseMove += (_, e) =>
            {
                if (element.IsMouseCaptured) moved(e.GetPosition(element));
            };
            element.MouseLeftButtonUp += (_, _) => element.ReleaseMouseCapture();
        }

        private void Refresh(bool fromHex = false)
        {
            _updating = true;
            _svBox.Background = new SolidColorBrush(FromHsv(_h, 1, 1));
            Canvas.SetLeft(_svThumb, _s * SvWidth - 8);
            Canvas.SetTop(_svThumb, (1 - _v) * SvHeight - 8);
            Canvas.SetLeft(_hueThumb, _h / 360 * SvWidth - 4);
            var c = Selected;
            _svThumb.Fill = new SolidColorBrush(c);
            _newSwatch.Background = new SolidColorBrush(c);
            if (!fromHex) _hex.Text = ToHex(c);
            _updating = false;
        }
    }
}

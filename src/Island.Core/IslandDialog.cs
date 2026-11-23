using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Island.Core;

/// <summary>
/// 和設定視窗同一套深色風格的提示視窗(取代 Windows 白色的 MessageBox)。
/// <c>IslandDialog.Show(owner, "訊息")</c> 只有「好」;<c>IslandDialog.Confirm(owner, "要…嗎?", "重新啟動")</c> 回傳是否按了確認。
/// </summary>
public static class IslandDialog
{
    public static void Show(Window? owner, string message, string title = "Island") =>
        Run(owner, title, message, okText: "好", cancelText: null);

    public static bool Confirm(Window? owner, string message, string okText = "確定", string cancelText = "取消", string title = "Island") =>
        Run(owner, title, message, okText, cancelText);

    private static bool Run(Window? owner, string title, string message, string okText, string? cancelText)
    {
        var window = new DialogWindow(title, message, okText, cancelText);
        if (owner is { IsLoaded: true }) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return window.ShowDialog() == true;
    }

    private sealed class DialogWindow : Window
    {
        public DialogWindow(string title, string message, string okText, string? cancelText)
        {
            Title = title;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Microsoft JhengHei UI, Segoe UI");

            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18), Width = 340 };

            // 標題列(可拖動)
            var head = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 10),
            };
            head.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            root.Children.Add(head);

            root.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xDC)),
                FontSize = 13,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            if (cancelText is not null)
            {
                var cancel = new Button { Content = cancelText, IsCancel = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
                if (Application.Current?.TryFindResource(IslandTheme.SettingsButton) is Style b1) cancel.Style = b1;
                cancel.Click += (_, _) => DialogResult = false;
                buttons.Children.Add(cancel);
            }
            var ok = new Button { Content = okText, IsDefault = true, IsCancel = cancelText is null, MinWidth = 76 };
            if (Application.Current?.TryFindResource(IslandTheme.SettingsAccentButton) is Style b2) ok.Style = b2;
            ok.Click += (_, _) => DialogResult = true;
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
            Loaded += (_, _) => ok.Focus();
        }
    }
}

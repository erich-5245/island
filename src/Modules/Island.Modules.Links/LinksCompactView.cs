using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Links;

/// <summary>
/// 捷徑小圓:顯示「一個」捷徑的圖示(設定裡標星號的那個,沒標就用第一個)。
/// 點小圓直接切到 / 打開那個視窗(LinksModule.OnCompactClick)。清單改變時才重畫,平常不耗資源。
/// </summary>
internal sealed class LinksCompactView : Grid
{
    private readonly LinksModule _module;

    public LinksCompactView(LinksModule module)
    {
        _module = module;
        Width = 30;
        Height = 30;
        Background = Brushes.Transparent;
        module.Items.CollectionChanged += OnItemsChanged;
        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    internal void Rebuild()
    {
        Children.Clear();
        var item = _module.CircleItem;
        ToolTip = item is null ? null : $"切到 {item.Name}";
        if (item?.Icon is { } icon)
        {
            var img = new Image
            {
                Source = icon,
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            Children.Add(img);
            return;
        }
        Children.Add(new TextBlock
        {
            Text = "",   // 連結
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }
}

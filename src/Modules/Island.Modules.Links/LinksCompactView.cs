using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Links;

/// <summary>
/// 捷徑小圓:第 index 個標星號的捷徑圖示(都沒標就用第一個,最多兩個小圓)。
/// 點小圓直接切到 / 打開那個視窗(LinksModule.RunCompactAction)。清單改變時才重畫,平常不耗資源。
/// </summary>
internal sealed class LinksCompactView : Grid
{
    private readonly LinksModule _module;
    private readonly int _index;

    public LinksCompactView(LinksModule module, int index)
    {
        _module = module;
        _index = index;
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
        var items = _module.CircleItems;
        var item = _index < items.Count ? items[_index] : null;
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

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Island.Modules.Links;

/// <summary>
/// 捷徑小圓:前四個捷徑的圖示排成 2×2(只有一個時放大置中)。點小圓由外殼打開「捷徑」分頁。
/// 清單改變時才重畫,平常不耗資源。
/// </summary>
internal sealed class LinksCompactView : Grid
{
    private readonly LinksModule _module;

    public LinksCompactView(LinksModule module)
    {
        _module = module;
        Width = 30;
        Height = 30;
        ToolTip = "捷徑";
        module.Items.CollectionChanged += OnItemsChanged;
        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        var icons = _module.Items.Select(i => i.Icon).Where(i => i is not null).Take(4).ToList();
        if (icons.Count == 0)
        {
            Children.Add(new TextBlock
            {
                Text = "",   // 全部應用程式
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }
        if (icons.Count == 1)
        {
            Children.Add(Icon(icons[0]!, 18));
            return;
        }

        var grid = new UniformGrid2x2();
        foreach (var icon in icons) grid.Children.Add(Icon(icon!, 11));
        Children.Add(grid);
    }

    private static Image Icon(ImageSource source, double size)
    {
        var img = new Image
        {
            Source = source,
            Width = size,
            Height = size,
            Margin = new Thickness(0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    /// <summary>置中的 2×2 格子(圖示只有 2、3 個時也維持同樣大小)。</summary>
    private sealed class UniformGrid2x2 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid2x2()
        {
            Rows = 2;
            Columns = 2;
            Width = 24;
            Height = 24;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
        }
    }
}

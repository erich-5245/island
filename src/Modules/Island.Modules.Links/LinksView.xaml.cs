using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Island.Modules.Links;

public partial class LinksView : UserControl
{
    private readonly LinksModule _module;

    internal LinksView(LinksModule module)
    {
        _module = module;
        InitializeComponent();
        ItemsList.ItemsSource = module.Items;
        module.Items.CollectionChanged += OnItemsChanged;
        UpdateEmptyState();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState() =>
        EmptyState.Visibility = _module.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private static LinkItem? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as LinkItem;

    private void OnTileClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemOf(sender) is { } item) _module.Open(item);
        e.Handled = true;
    }

    private void OnMoveLeft(object sender, RoutedEventArgs e) { if (ItemOf(sender) is { } i) _module.Move(i, -1); }
    private void OnMoveRight(object sender, RoutedEventArgs e) { if (ItemOf(sender) is { } i) _module.Move(i, +1); }
    private void OnRemove(object sender, RoutedEventArgs e) { if (ItemOf(sender) is { } i) _module.Remove(i); }
}

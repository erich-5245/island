using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Island.Modules.Shelf;

public partial class ShelfView : UserControl
{
    private readonly ShelfModule _module;
    private Point? _dragStart;
    private IReadOnlyList<ShelfItem> _dragItems = [];

    internal ShelfView(ShelfModule module)
    {
        _module = module;
        InitializeComponent();
        ItemsList.ItemsSource = module.Items;
        module.Items.CollectionChanged += (_, _) => UpdateState();
        UpdateState();
    }

    private void UpdateState()
    {
        int n = _module.Items.Count;
        CountText.Text = $"{n} 個項目";
        Header.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = n > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── 點擊與拖出 ───────────────────────────────────────

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShelfItem item) return;
        if (e.ClickCount == 2)
        {
            _dragStart = null;
            _module.Open(item);
            e.Handled = true;
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragItems = [item];
    }

    private void OnDragAllMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragItems = _module.Items.ToList();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragStart = null;
        var items = _dragItems;
        _dragItems = [];
        _module.StartDrag(items, this);
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e) => _dragStart = null;

    // ── 選單與按鈕 ───────────────────────────────────────

    private static ShelfItem? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as ShelfItem;

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _module.Open(item);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _module.Reveal(item);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _module.Remove(item);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _module.Copy([item]);
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => _module.Clear();

    private void OnPasteClick(object sender, RoutedEventArgs e) => _module.Paste();
}

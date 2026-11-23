using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.Shelf;

/// <summary>
/// Shelf(暫存架):把檔案、文字、網址、圖片拖到膠囊上暫放,之後再從展開畫面拖到別的地方。
/// 檔案只保存路徑(不複製);文字 / 網址存在 shelf.json;圖片存成 modules\shelf\clips 裡的檔案。
/// 清單存在 %LOCALAPPDATA%\Island\modules\shelf\shelf.json。
/// 閒置時不做任何事:沒有計時器;展開時才檢查檔案是否還存在。
/// 截圖(Win+Shift+S 自動存檔)會自動放進 Shelf,膠囊跳出提示(modules\shelf\options.json 可關閉)。
/// </summary>
public sealed class ShelfModule : IslandModuleBase, IIslandDropTarget, IIslandExpandAware, IIslandCompact, IIslandActivity, IIslandSettingsPage
{
    private string _storeFile = "";
    private ShelfView? _view;
    private System.Windows.Controls.Grid? _compact;
    private System.Windows.Controls.TextBlock? _compactCount;

    public override string Id => "shelf";
    public override string Title => "Shelf";

    public ObservableCollection<ShelfItem> Items { get; } = [];

    // ── 截圖自動放進 Shelf ───────────────────────────────
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(3);
    private readonly ScreenshotWatcher _screenshots = new();
    private ScreenshotToastView? _toast;
    private DispatcherTimer? _toastTimer;
    private bool _toastActive;

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _toastActive;
    public FrameworkElement CreateActivityView() => _toast ??= new ScreenshotToastView();

    protected override void OnInitialize()
    {
        _storeFile = Path.Combine(Host.GetModuleDataDirectory(Id), "shelf.json");
        Load();
        PruneMissing(save: true);
        UpdateBadge();

        _options = ShelfOptions.Load(Host.GetModuleDataDirectory(Id));
        _toastTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = ToastDuration + TimeSpan.FromMilliseconds(300) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); SetToast(false); };
        _screenshots.Captured += path => Host.Dispatcher.BeginInvoke(() => OnScreenshot(path));
        if (_options.CaptureScreenshots) StartCapture();
    }

    private ShelfOptions _options = new();

    private void StartCapture()
    {
        bool ok = _screenshots.Start();
        Host.Log(ok ? $"Shelf:監看截圖資料夾 {_screenshots.Folder}" : "Shelf:找不到截圖資料夾,截圖不會自動加入");
    }

    // ── 設定頁 ──
    public string SettingsTitle => "Shelf";

    public FrameworkElement CreateSettingsView()
    {
        var card = new System.Windows.Controls.Border();
        card.SetResourceReference(StyleProperty, IslandTheme.SettingsCard);
        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
        var text = new System.Windows.Controls.StackPanel();
        var title = new System.Windows.Controls.TextBlock { Text = "截圖自動放進 Shelf" };
        title.SetResourceReference(StyleProperty, IslandTheme.SettingsTitleText);
        var caption = new System.Windows.Controls.TextBlock
        {
            Text = $"Win+Shift+S 截圖後會自動加入 Shelf,並在膠囊顯示提示。監看的資料夾:{_screenshots.Folder ?? "(啟用後會自動尋找)"}",
        };
        caption.SetResourceReference(StyleProperty, IslandTheme.SettingsCaptionText);
        text.Children.Add(title);
        text.Children.Add(caption);
        grid.Children.Add(text);
        var toggle = new System.Windows.Controls.CheckBox { IsChecked = _options.CaptureScreenshots, Margin = new Thickness(16, 0, 0, 0) };
        toggle.SetResourceReference(StyleProperty, IslandTheme.SettingsToggle);
        toggle.Checked += (_, _) => SetCapture(true);
        toggle.Unchecked += (_, _) => SetCapture(false);
        System.Windows.Controls.Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        card.Child = grid;
        return card;
    }

    private static readonly DependencyProperty StyleProperty = FrameworkElement.StyleProperty;

    private void SetCapture(bool on)
    {
        _options.CaptureScreenshots = on;
        _options.Save(Host.GetModuleDataDirectory(Id));
        if (on) StartCapture();
        else _screenshots.Stop();
    }

    private void OnScreenshot(string path)
    {
        int before = Items.Count;
        Add([path]);
        if (Items.Count == before) return;   // 已經在 Shelf 裡
        _toast ??= new ScreenshotToastView();
        _toast.Show(path, Items.Count);
        SetToast(true);
        Host.Announce(Id, ToastDuration);
        _toastTimer!.Stop();
        _toastTimer.Start();
    }

    private void SetToast(bool active)
    {
        if (_toastActive == active) return;
        _toastActive = active;
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void Dispose()
    {
        _toastTimer?.Stop();
        _screenshots.Dispose();
        base.Dispose();
    }

    public override FrameworkElement CreateExpandedView() => _view ??= new ShelfView(this);

    public bool IsCompactRelevant => Items.Count > 0;

    /// <summary>小圓:檔案圖示 + 右上角數量。</summary>
    public FrameworkElement CreateCompactView()
    {
        if (_compact is not null) return _compact;
        _compact = new System.Windows.Controls.Grid { Width = 30, Height = 30 };
        _compact.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "",   // 文件
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 13,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var badge = new System.Windows.Controls.Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(3, 0, 3, 0),
            MinWidth = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        badge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, IslandTheme.Accent);
        _compactCount = new System.Windows.Controls.TextBlock { FontSize = 9, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
        _compactCount.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, IslandTheme.OnAccent);
        badge.Child = _compactCount;
        _compact.Children.Add(badge);
        UpdateCompact();
        return _compact;
    }

    private void UpdateCompact()
    {
        if (_compactCount is not null) _compactCount.Text = Items.Count > 99 ? "99+" : Items.Count.ToString();
    }

    // ── 拖進來 / 貼上 ─────────────────────────────────────

    private const string FileGroupDescriptor = "FileGroupDescriptorW";
    private const string FileContents = "FileContents";

    private string ClipsDirectory => Path.Combine(Host.GetModuleDataDirectory(Id), "clips");

    public bool CanAccept(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) ||
        data.GetDataPresent(FileGroupDescriptor) ||
        data.GetDataPresent(DataFormats.Bitmap) ||
        data.GetDataPresent(DataFormats.UnicodeText);

    /// <summary>
    /// 依序嘗試:檔案 → 虛擬檔案(瀏覽器拖出的圖片、Outlook 附件)→ 圖片 → 文字 / 網址。
    /// 先成功的那種就用,同一次拖曳不會加入兩份。
    /// </summary>
    public void Accept(IDataObject data)
    {
        try
        {
            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                Add(paths);
                return;
            }
            if (data.GetDataPresent(FileGroupDescriptor) && TryAddVirtualFile(data)) return;
            if (data.GetDataPresent(DataFormats.Bitmap) && TryAddBitmap(data)) return;
            if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string { Length: > 0 } text)
                AddItems([ShelfItem.FromText(text)]);
        }
        catch (Exception ex)
        {
            Host.Log($"Shelf:加入失敗:{ex.Message}");
        }
    }

    /// <summary>把剪貼簿的內容加進 Shelf(面板上的「貼上」按鈕)。</summary>
    public void Paste()
    {
        try
        {
            if (Clipboard.GetDataObject() is { } data && CanAccept(data)) Accept(data);
        }
        catch (Exception ex)
        {
            Host.Log($"Shelf:讀取剪貼簿失敗:{ex.Message}");
        }
    }

    private bool TryAddVirtualFile(IDataObject data)
    {
        if (data.GetData(FileGroupDescriptor) is not MemoryStream descriptor) return false;
        var name = ShelfClips.ReadFileGroupName(descriptor);
        if (name is null || data.GetData(FileContents) is not MemoryStream contents || contents.Length == 0) return false;
        Directory.CreateDirectory(ClipsDirectory);
        var path = ShelfClips.UniquePath(ClipsDirectory, name);
        using (var f = File.Create(path))
        {
            contents.Position = 0;
            contents.CopyTo(f);
        }
        AddItems([ShelfItem.FromPath(path, owned: true)]);
        return true;
    }

    private bool TryAddBitmap(IDataObject data)
    {
        if (data.GetData(DataFormats.Bitmap) is not BitmapSource bmp) return false;
        Directory.CreateDirectory(ClipsDirectory);
        var path = ShelfClips.UniquePath(ClipsDirectory, $"圖片 {DateTime.Now:yyyy-MM-dd HHmmss}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using (var f = File.Create(path)) encoder.Save(f);
        AddItems([ShelfItem.FromPath(path, owned: true)]);
        return true;
    }

    public void Add(IEnumerable<string> paths)
    {
        var items = new List<ShelfItem>();
        foreach (var raw in paths)
        {
            string full;
            try { full = Path.GetFullPath(raw); }
            catch { continue; }
            items.Add(ShelfItem.FromPath(full, owned: ShelfClips.IsInside(full, ClipsDirectory)));
        }
        AddItems(items);
    }

    private void AddItems(IEnumerable<ShelfItem> items)
    {
        int added = 0;
        foreach (var item in items)
        {
            if (!item.Exists || Items.Any(i => i.SameAs(item))) continue;
            Items.Add(item);
            added++;
        }
        if (added == 0) return;
        Host.Log($"Shelf:加入 {added} 個項目,共 {Items.Count} 個");
        Changed();
    }

    // ── 拖出去 / 複製 ─────────────────────────────────────

    /// <summary>檔案 → FileDrop;文字 / 網址 → 文字(多個用換行連起來)。</summary>
    private static DataObject BuildData(IReadOnlyList<ShelfItem> items, bool forClipboard)
    {
        var data = new DataObject();
        var files = items.Where(i => i.IsFileBased).Select(i => i.Path).ToArray();
        if (files.Length > 0) data.SetData(DataFormats.FileDrop, files);
        var texts = items.Where(i => !i.IsFileBased).Select(i => i.Text).ToList();
        if (texts.Count > 0) data.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, texts));

        // 複製單張圖片:同時放圖片本身,貼到聊天軟體 / 小畫家都可以
        if (forClipboard && items.Count == 1 && items[0].Kind == ShelfKind.Image)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(items[0].Path);
                bmp.EndInit();
                data.SetImage(bmp);
            }
            catch { /* 讀不到圖就只放檔案 */ }
        }
        return data;
    }

    /// <summary>
    /// 把項目拖到其他程式。行為和從檔案總管拖曳相同:
    /// 同一個磁碟預設為「移動」,不同磁碟為「複製」,按住 Ctrl/Shift/Alt 可切換。
    /// Shelf 自己存的圖片、文字和網址只會複製,拖出後仍留在 Shelf。
    /// 拖曳結束後,已經不存在(被移走)的項目會自動從 Shelf 移除。
    /// </summary>
    public void StartDrag(IReadOnlyList<ShelfItem> items, DependencyObject source)
    {
        if (items.Count == 0) return;
        var data = BuildData(items, forClipboard: false);
        var allowed = items.Any(i => i.IsOwned || !i.IsFileBased)
            ? DragDropEffects.Copy | DragDropEffects.Link
            : DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;

        DragDropEffects result;
        using (Host.HoldExpanded())
        {
            result = DragDrop.DoDragDrop(source, data, allowed);
        }
        Host.Log($"Shelf:拖出 {items.Count} 個項目,結果 {result}");

        // 檔案總管的移動可能在背景完成,稍等一下再檢查
        PruneMissing(save: true);
        var timer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) => { timer.Stop(); PruneMissing(save: true); };
        timer.Start();
    }

    public void Copy(IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0) return;
        try { Clipboard.SetDataObject(BuildData(items, forClipboard: true), copy: true); }
        catch (Exception ex) { Host.Log($"Shelf:複製到剪貼簿失敗:{ex.Message}"); }
    }

    // ── 項目操作 ─────────────────────────────────────────

    /// <summary>檔案 / 圖片 / 網址 → 開啟;文字 → 複製到剪貼簿。</summary>
    public void Open(ShelfItem item)
    {
        if (item.Kind == ShelfKind.Text)
        {
            Copy([item]);
            return;
        }
        var target = item.Kind == ShelfKind.Url ? item.Text : item.Path;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Host.Log($"Shelf:開啟失敗 {target}:{ex.Message}"); }
    }

    public void Reveal(ShelfItem item)
    {
        if (!item.IsFileBased) return;
        try { Process.Start("explorer.exe", $"/select,\"{item.Path}\""); }
        catch (Exception ex) { Host.Log($"Shelf:顯示位置失敗 {item.Path}:{ex.Message}"); }
    }

    public void Remove(ShelfItem item)
    {
        if (!Items.Remove(item)) return;
        DeleteOwned([item]);
        Changed();
    }

    public void Clear()
    {
        if (Items.Count == 0) return;
        var all = Items.ToList();
        Items.Clear();
        DeleteOwned(all);
        Changed();
    }

    /// <summary>Shelf 自己存的檔案(clips 資料夾)在移除時刪掉,不留垃圾。</summary>
    private void DeleteOwned(IEnumerable<ShelfItem> items)
    {
        foreach (var i in items.Where(i => i.IsOwned && ShelfClips.IsInside(i.Path, ClipsDirectory)))
        {
            try { File.Delete(i.Path); }
            catch (Exception ex) { Host.Log($"Shelf:刪除暫存檔失敗 {i.Path}:{ex.Message}"); }
        }
    }

    // ── 展開通知 ─────────────────────────────────────────

    public void OnExpanded() => PruneMissing(save: true);

    public void OnCollapsed() { }

    // ── 內部 ─────────────────────────────────────────────

    private void PruneMissing(bool save)
    {
        var missing = Items.Where(i => !i.Exists).ToList();
        if (missing.Count == 0) return;
        foreach (var m in missing) Items.Remove(m);
        Host.Log($"Shelf:移除 {missing.Count} 個已不存在的項目");
        if (save) Changed();
    }

    private void Changed()
    {
        Save();
        UpdateBadge();
    }

    private void UpdateBadge()
    {
        UpdateCompact();
        SetBadge(Items.Count == 0 ? null : new IslandBadge($"Shelf {Items.Count}"));
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storeFile)) return;
            foreach (var r in ShelfClips.Deserialize(File.ReadAllText(_storeFile)))
            {
                var item = ShelfItem.FromRecord(r);
                if (!Items.Any(i => i.SameAs(item))) Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Host.Log($"Shelf:讀取 shelf.json 失敗:{ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_storeFile, ShelfClips.Serialize(Items.Select(i => i.ToRecord())));
        }
        catch (Exception ex)
        {
            Host.Log($"Shelf:寫入 shelf.json 失敗:{ex.Message}");
        }
    }
}

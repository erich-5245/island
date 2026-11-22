using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.Shelf;

/// <summary>
/// Shelf(檔案暫存架):把檔案拖到膠囊上暫放,之後再從展開畫面拖到別的地方。
/// 只保存路徑(不複製檔案),清單存在 %LOCALAPPDATA%\Island\modules\shelf\shelf.json。
/// 閒置時不做任何事:沒有計時器;展開時才檢查檔案是否還存在。
/// 截圖(Win+Shift+S 自動存檔)會自動放進 Shelf,膠囊跳出提示(modules\shelf\options.json 可關閉)。
/// </summary>
public sealed class ShelfModule : IslandModuleBase, IIslandDropTarget, IIslandExpandAware, IIslandCompact, IIslandActivity
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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

        if (ShelfOptions.Load(Host.GetModuleDataDirectory(Id)).CaptureScreenshots)
        {
            _toastTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = ToastDuration + TimeSpan.FromMilliseconds(300) };
            _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); SetToast(false); };
            _screenshots.Captured += path => Host.Dispatcher.BeginInvoke(() => OnScreenshot(path));
            bool ok = _screenshots.Start();
            Host.Log(ok ? $"Shelf:監看截圖資料夾 {_screenshots.Folder}" : "Shelf:找不到截圖資料夾,截圖不會自動加入");
        }
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
            Text = "\uE8A5",   // 文件
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

    // ── 拖進來 ───────────────────────────────────────────

    public bool CanAccept(IDataObject data) => data.GetDataPresent(DataFormats.FileDrop);

    public void Accept(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] paths) Add(paths);
    }

    public void Add(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var raw in paths)
        {
            string full;
            try { full = Path.GetFullPath(raw); }
            catch { continue; }

            if (Items.Any(i => string.Equals(i.Path, full, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new ShelfItem(full);
            if (!item.Exists) continue;
            Items.Add(item);
            added++;
        }
        if (added == 0) return;
        Host.Log($"Shelf:加入 {added} 個項目,共 {Items.Count} 個");
        Changed();
    }

    // ── 拖出去 ───────────────────────────────────────────

    /// <summary>
    /// 把項目拖到其他程式。行為和從檔案總管拖曳相同:
    /// 同一個磁碟預設為「移動」,不同磁碟為「複製」,按住 Ctrl/Shift/Alt 可切換。
    /// 拖曳結束後,已經不存在(被移走)的項目會自動從 Shelf 移除。
    /// </summary>
    public void StartDrag(IReadOnlyList<ShelfItem> items, DependencyObject source)
    {
        if (items.Count == 0) return;
        var data = new DataObject(DataFormats.FileDrop, items.Select(i => i.Path).ToArray());

        DragDropEffects result;
        using (Host.HoldExpanded())
        {
            result = DragDrop.DoDragDrop(source, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        Host.Log($"Shelf:拖出 {items.Count} 個項目,結果 {result}");

        // 檔案總管的移動可能在背景完成,稍等一下再檢查
        PruneMissing(save: true);
        var timer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1.5) };
        timer.Tick += (_, _) => { timer.Stop(); PruneMissing(save: true); };
        timer.Start();
    }

    // ── 項目操作 ─────────────────────────────────────────

    public void Open(ShelfItem item)
    {
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) { Host.Log($"Shelf:開啟失敗 {item.Path}:{ex.Message}"); }
    }

    public void Reveal(ShelfItem item)
    {
        try { Process.Start("explorer.exe", $"/select,\"{item.Path}\""); }
        catch (Exception ex) { Host.Log($"Shelf:顯示位置失敗 {item.Path}:{ex.Message}"); }
    }

    public void Remove(ShelfItem item)
    {
        if (Items.Remove(item)) Changed();
    }

    public void Clear()
    {
        if (Items.Count == 0) return;
        Items.Clear();
        Changed();
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
            var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_storeFile)) ?? [];
            foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase)) Items.Add(new ShelfItem(p));
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
            File.WriteAllText(_storeFile, JsonSerializer.Serialize(Items.Select(i => i.Path).ToList(), JsonOptions));
        }
        catch (Exception ex)
        {
            Host.Log($"Shelf:寫入 shelf.json 失敗:{ex.Message}");
        }
    }
}

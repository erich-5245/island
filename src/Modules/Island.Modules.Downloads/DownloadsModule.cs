using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.Downloads;

/// <summary>
/// 下載進度:監看「下載」資料夾(FileSystemWatcher,事件驅動)。
/// - 瀏覽器下載中的暫存檔(Chrome / Edge 的 .crdownload、Firefox 的 .part)出現 → 有一個下載進行中;
///   滑鼠移過去顯示「⬇ 下載中 · 檔名 · 12.3 MB」(大小每秒更新一次,只在看得到時更新)。
/// - 暫存檔改名成正式檔名 / Firefox 刪掉 .part → 下載完成:膠囊出現「下載完成 · 檔名」,並自動放進 Shelf。
/// 瀏覽器沒有告訴檔案系統總大小,所以顯示的是「已下載多少」而不是百分比。
/// </summary>
public sealed class DownloadsModule : IslandModuleBase, IIslandActivity, IIslandCompact, IIslandSettingsPage
{
    private static readonly string[] PartialExtensions = [".crdownload", ".part", ".partial", ".download"];
    private static readonly TimeSpan DoneShowFor = TimeSpan.FromSeconds(4);

    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _sizeTimer;
    private DispatcherTimer? _doneTimer;
    private readonly Dictionary<string, DateTime> _active = new(StringComparer.OrdinalIgnoreCase);   // 暫存檔 → 開始時間
    private DownloadRow? _row;
    private DownloadBadge? _badge;
    private Options _options = new();

    public override string Id => "downloads";
    public override string Title => "下載";
    public string SettingsTitle => "下載";

    public string? Folder { get; private set; }
    /// <summary>剛完成的檔案(完成提示期間有值)。</summary>
    public string? JustCompleted { get; private set; }

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _active.Count > 0 || JustCompleted is not null;
    public bool IsCompactRelevant => _active.Count > 0;

    /// <summary>點小圓「直接執行」(設定 → 即時狀態可以選;預設是打開分頁)。</summary>
    public string? CompactActionLabel => "打開下載資料夾";
    public void RunCompactAction(int index) { if (Folder is not null) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Folder) { UseShellExecute = true }); } catch (Exception ex) { Host.Log($"小圓動作失敗:{ex.Message}"); } } }

    /// <summary>目前下載中的暫存檔(依開始時間)。</summary>
    public IReadOnlyList<string> Active => _active.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

    protected override void OnInitialize()
    {
        _options = Options.Load(Host.GetModuleDataDirectory(Id));
        _sizeTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _sizeTimer.Tick += (_, _) => RefreshViews(tickOnly: true);
        _doneTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = DoneShowFor + TimeSpan.FromMilliseconds(300) };
        _doneTimer.Tick += (_, _) => { _doneTimer.Stop(); JustCompleted = null; Changed(); };

        Folder = DownloadsFolder();
        if (Folder is null)
        {
            Host.Log("下載:找不到下載資料夾");
            return;
        }
        try
        {
            _watcher = new FileSystemWatcher(Folder) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName, InternalBufferSize = 16384 };
            _watcher.Created += (_, e) => Post(() => OnCreated(e.FullPath));
            _watcher.Renamed += (_, e) => Post(() => OnRenamed(e.OldFullPath, e.FullPath));
            _watcher.Deleted += (_, e) => Post(() => OnDeleted(e.FullPath));
            _watcher.EnableRaisingEvents = true;
            // 啟動時已經在下載中的(例如重開 Island)
            foreach (var f in Directory.EnumerateFiles(Folder).Where(IsPartial)) _active[f] = File.GetCreationTime(f);
            Host.Log($"下載:監看 {Folder}");
        }
        catch (Exception ex)
        {
            Host.Log($"下載:無法監看下載資料夾:{ex.Message}");
        }
        Changed();
    }

    public FrameworkElement CreateActivityView() => _row ??= new DownloadRow(this);
    public FrameworkElement CreateCompactView() => _badge ??= new DownloadBadge(this);

    private void Post(Action a) => Host.Dispatcher.BeginInvoke(a);

    private static bool IsPartial(string path) =>
        PartialExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void OnCreated(string path)
    {
        if (!IsPartial(path)) return;
        _active[path] = DateTime.Now;
        Changed();
    }

    private void OnRenamed(string oldPath, string newPath)
    {
        bool wasPartial = _active.Remove(oldPath) || IsPartial(oldPath);
        if (IsPartial(newPath))
        {
            // Chrome:「未確認 123.crdownload」→「檔名.zip.crdownload」,還在下載
            _active[newPath] = DateTime.Now;
            Changed();
            return;
        }
        if (wasPartial) Completed(newPath);
    }

    private void OnDeleted(string path)
    {
        if (!_active.Remove(path)) return;
        // Firefox:下載完成時刪掉「檔名.part」,正式檔案「檔名」已經在了
        var final = path[..^Path.GetExtension(path).Length];
        if (File.Exists(final) && new FileInfo(final).Length > 0) Completed(final);
        else Changed();   // 取消下載
    }

    private void Completed(string path)
    {
        Host.Log($"下載:完成 {Path.GetFileName(path)}");
        JustCompleted = path;
        if (_options.AddToShelf)
            Host.SendToModule("shelf", new DataObject(DataFormats.FileDrop, new[] { path }));
        Changed();
        Host.Announce(Id, DoneShowFor);
        _doneTimer!.Stop();
        _doneTimer.Start();
    }

    private bool _wasActive;

    private void Changed()
    {
        RefreshViews(tickOnly: false);
        if (_active.Count > 0) { if (!_sizeTimer!.IsEnabled) _sizeTimer.Start(); }
        else _sizeTimer?.Stop();
        if (_wasActive != IsActivityActive)
        {
            _wasActive = IsActivityActive;
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshViews(bool tickOnly)
    {
        if (_row is not null && (!tickOnly || _row.IsVisible)) _row.Update();
        if (_badge is not null && (!tickOnly || _badge.IsVisible)) _badge.Update();
    }

    /// <summary>暫存檔 → 顯示用的檔名(去掉 .crdownload 等;Chrome 一開始的「未確認 123」照原樣)。</summary>
    public static string DisplayName(string partial)
    {
        var name = Path.GetFileName(partial);
        var ext = Path.GetExtension(name);
        return PartialExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ? name[..^ext.Length] : name;
    }

    public static long SizeOf(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };

    // ── 設定頁 ──
    public FrameworkElement CreateSettingsView()
    {
        var card = new Border();
        card.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCard);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        var title = new TextBlock { Text = "下載完成後放進 Shelf" };
        title.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsTitleText);
        var caption = new TextBlock { Text = $"瀏覽器下載完成時,檔案自動加進 Shelf,方便直接拖到別的地方。監看的資料夾:{Folder ?? "(找不到)"}" };
        caption.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCaptionText);
        text.Children.Add(title);
        text.Children.Add(caption);
        grid.Children.Add(text);
        var toggle = new CheckBox { IsChecked = _options.AddToShelf, Margin = new Thickness(16, 0, 0, 0) };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsToggle);
        toggle.Checked += (_, _) => { _options.AddToShelf = true; _options.Save(Host.GetModuleDataDirectory(Id)); };
        toggle.Unchecked += (_, _) => { _options.AddToShelf = false; _options.Save(Host.GetModuleDataDirectory(Id)); };
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        card.Child = grid;
        return card;
    }

    private static string? DownloadsFolder()
    {
        try
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var ptr) == 0)
            {
                var path = Marshal.PtrToStringUni(ptr);
                Marshal.FreeCoTaskMem(ptr);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
            }
        }
        catch { }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(fallback) ? fallback : null;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

    public override void Dispose()
    {
        _sizeTimer?.Stop();
        _doneTimer?.Stop();
        _watcher?.Dispose();
        base.Dispose();
    }

    private sealed class Options
    {
        public bool AddToShelf { get; set; } = true;
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static Options Load(string dir)
        {
            var f = Path.Combine(dir, "options.json");
            try
            {
                if (File.Exists(f)) return JsonSerializer.Deserialize<Options>(File.ReadAllText(f), Json) ?? new Options();
                var o = new Options();
                o.Save(dir);
                return o;
            }
            catch { return new Options(); }
        }

        public void Save(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "options.json"), JsonSerializer.Serialize(this, Json));
            }
            catch { }
        }
    }
}

/// <summary>一排:[⬇] 下載中 · 檔名 ......... 12.3 MB(多個時顯示「3 個」);完成時 [✓] 下載完成 · 檔名。</summary>
internal sealed class DownloadRow : Grid
{
    private readonly DownloadsModule _module;
    private readonly TextBlock _icon = new() { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 9, 0) };
    private readonly TextBlock _title = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _size = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 2, 0) };

    public DownloadRow(DownloadsModule module)
    {
        _module = module;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        VerticalAlignment = VerticalAlignment.Center;
        _icon.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        Typography.SetNumeralAlignment(_size, FontNumeralAlignment.Tabular);
        Children.Add(_icon);
        SetColumn(_title, 1); Children.Add(_title);
        SetColumn(_size, 2); Children.Add(_size);
        IsVisibleChanged += (_, _) => { if (IsVisible) Update(); };
        Update();
    }

    public void Update()
    {
        var active = _module.Active;
        if (_module.JustCompleted is { } done && active.Count == 0)
        {
            _icon.Text = "\uE73E";   // 打勾
            _title.Text = $"下載完成 · {Path.GetFileName(done)}";
            _size.Text = DownloadsModule.FormatSize(DownloadsModule.SizeOf(done));
            return;
        }
        _icon.Text = "\uE896";       // 下載箭頭
        if (active.Count == 0) { _title.Text = ""; _size.Text = ""; return; }
        _title.Text = active.Count == 1 ? $"下載中 · {DownloadsModule.DisplayName(active[0])}" : $"下載中 · {active.Count} 個檔案";
        _size.Text = DownloadsModule.FormatSize(active.Sum(DownloadsModule.SizeOf));
    }
}

/// <summary>小圓:下載箭頭 + 已下載大小(例如「12M」)。</summary>
internal sealed class DownloadBadge : StackPanel
{
    private readonly DownloadsModule _module;
    private readonly TextBlock _size = new() { FontSize = 8.5, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };

    public DownloadBadge(DownloadsModule module)
    {
        _module = module;
        VerticalAlignment = VerticalAlignment.Center;
        var arrow = new TextBlock { Text = "\uE896", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
        arrow.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        Children.Add(arrow);
        Children.Add(_size);
        IsVisibleChanged += (_, _) => { if (IsVisible) Update(); };
        Update();
    }

    public void Update()
    {
        long bytes = _module.Active.Sum(DownloadsModule.SizeOf);
        _size.Text = bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.#}G" : bytes >= 1L << 20 ? $"{bytes >> 20}M" : $"{bytes >> 10}K";
    }
}

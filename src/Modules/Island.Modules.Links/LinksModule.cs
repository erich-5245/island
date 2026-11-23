using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Links;

public enum LinkKind { App, Shortcut, Folder, File, Url }

/// <summary>一個捷徑。Exe = 用來找「已經開著的視窗」的程式路徑(捷徑 .lnk 會解析出目標程式)。</summary>
public sealed class LinkItem
{
    public LinkKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Exe { get; set; }
    /// <summary>這個捷徑是滑鼠移到島上時顯示的小圓(只會有一個)。</summary>
    public bool Circle { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? Icon { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Detail => Kind == LinkKind.Url ? Target : Exe ?? Target;
}

/// <summary>
/// 捷徑:展開面板的「捷徑」分頁放常用的程式 / 資料夾 / 網址。
/// 點一下:已經開著 → 把視窗切到最前面(最小化的先還原);沒開 → 打開。
/// 加入方式:設定頁從「開著的視窗」挑、瀏覽選檔、輸入網址,或在捷徑分頁開著時把捷徑 / 程式 / 資料夾拖到島上。
/// 清單存在 %LOCALAPPDATA%\Island\modules\links\links.json。平常不耗資源。
/// </summary>
public sealed class LinksModule : IslandModuleBase, IIslandDropTarget, IIslandSettingsPage, IIslandCompact, IIslandCompactStrip, IIslandNumberHotkeys
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private string _file = "";
    private LinksView? _view;

    public override string Id => "links";
    public override string Title => "捷徑";
    public string SettingsTitle => "捷徑";

    public ObservableCollection<LinkItem> Items { get; } = [];

    protected override void OnInitialize()
    {
        _file = Path.Combine(Host.GetModuleDataDirectory(Id), "links.json");
        _optionsFile = Path.Combine(Host.GetModuleDataDirectory(Id), "options.json");
        Load();
        LoadOptions();
    }

    public override FrameworkElement CreateExpandedView() => _view ??= new LinksView(this);
    public FrameworkElement CreateSettingsView() => new LinksSettingsView(this);

    // ── 小圓:每個代表「一個」捷徑,點一下直接切到 / 打開它;其他捷徑還是展開島在捷徑分頁點 ──
    public const int MaxCircles = 2;
    private readonly LinksCompactView?[] _compacts = new LinksCompactView?[MaxCircles];

    /// <summary>小圓代表的捷徑:設定裡標了星號的(最多兩個,依清單順序);都沒標就用第一個。</summary>
    public IReadOnlyList<LinkItem> CircleItems
    {
        get
        {
            var starred = Items.Where(i => i.Circle).Take(MaxCircles).ToList();
            if (starred.Count > 0) return starred;
            return Items.Count > 0 ? [Items[0]] : [];
        }
    }

    /// <summary>第一個小圓的捷徑(相容舊的呼叫)。</summary>
    public LinkItem? CircleItem => CircleItems.FirstOrDefault();

    public bool IsCompactRelevant => CircleItems.Count > 0;
    public bool ShowWithoutActivity => true;
    public int CompactCount => MaxCircles;
    public bool IsCompactRelevantAt(int index) => index < CircleItems.Count;
    public FrameworkElement CreateCompactView() => CreateCompactView(0);
    public FrameworkElement CreateCompactView(int index) => _compacts[index] ??= new LinksCompactView(this, index);

    public string? CompactActionLabel => "切到那個視窗";
    public bool CompactActionByDefault => true;

    public void RunCompactAction(int index)
    {
        var items = CircleItems;
        if (index < items.Count) Open(items[index]);
    }

    // ── 第一個小圓延伸:列出開著的視窗(滑鼠停在小圓上) ──

    private string _optionsFile = "";
    private WindowStripView? _strip;

    /// <summary>滑鼠停在第一個捷徑小圓上時,往外延伸列出開著的視窗。</summary>
    public bool WindowStrip { get; private set; } = true;

    public void SetWindowStrip(bool on)
    {
        WindowStrip = on;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_optionsFile)!);
            File.WriteAllText(_optionsFile, JsonSerializer.Serialize(new LinksOptions { WindowStrip = on }, JsonOptions));
        }
        catch (Exception ex) { Host.Log($"捷徑:儲存選項失敗:{ex.Message}"); }
    }

    private void LoadOptions()
    {
        try
        {
            if (File.Exists(_optionsFile))
                WindowStrip = JsonSerializer.Deserialize<LinksOptions>(File.ReadAllText(_optionsFile), JsonOptions)?.WindowStrip ?? true;
        }
        catch { WindowStrip = true; }
    }

    /// <summary>只有第一個小圓會延伸(兩個都拉長會擋到畫面)。</summary>
    public bool HasStrip(int index) => WindowStrip && index == 0 && CircleItems.Count > 0;

    public FrameworkElement? OpenStrip(int index, bool towardLeft, double height)
    {
        CloseStrip();
        if (!HasStrip(index)) return null;
        return _strip = new WindowStripView(this, CircleItems[0], towardLeft, height);
    }

    public void CloseStrip()
    {
        _strip?.Stop();
        _strip = null;
    }

    /// <summary>切到某個開著的視窗(延伸列上點一格)。</summary>
    public void SwitchTo(AppWindow window)
    {
        try
        {
            if (!WindowFinder.Activate(window.Handle)) Host.Log($"捷徑:切換到「{window.Title}」失敗");
            Host.Collapse();
        }
        catch (Exception ex) { Host.Log($"捷徑:切換視窗失敗:{ex.Message}"); }
    }

    /// <summary>
    /// 星號開 / 關。已經有兩個星號時再標一個,最早標的那個會被取消(依清單順序的第一個)。
    /// </summary>
    public void ToggleCircle(LinkItem item)
    {
        if (item.Circle) item.Circle = false;
        else
        {
            var starred = Items.Where(i => i.Circle).ToList();
            if (starred.Count >= MaxCircles) starred[0].Circle = false;
            item.Circle = true;
        }
        Save();
        RefreshCircles();
    }

    /// <summary>只把這一個設成小圓(其他取消)。</summary>
    public void SetCircle(LinkItem item)
    {
        foreach (var i in Items) i.Circle = ReferenceEquals(i, item);
        Save();
        RefreshCircles();
    }

    private void RefreshCircles()
    {
        foreach (var c in _compacts) c?.Rebuild();
        CircleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>小圓改成別的捷徑(設定頁用來更新星號)。</summary>
    public event EventHandler? CircleChanged;

    /// <summary>Ctrl+Alt+1…9:第 n 個捷徑。</summary>
    public void OnNumberHotkey(int number)
    {
        if (number >= 1 && number <= Items.Count) Open(Items[number - 1]);
    }

    // ── 開啟 / 切換 ─────────────────────────────────────

    public void Open(LinkItem item)
    {
        try
        {
            bool switched = item.Kind switch
            {
                LinkKind.App or LinkKind.Shortcut => SwitchToApp(item),
                LinkKind.Folder => SwitchToFolder(item.Target),
                _ => false,
            };
            if (!switched) Launch(item);
            Host.Log($"捷徑:{(switched ? "切換到" : "開啟")} {item.Name}");
            Host.Collapse();
        }
        catch (Exception ex)
        {
            Host.Log($"捷徑:開啟 {item.Name} 失敗:{ex.Message}");
        }
    }

    private static bool SwitchToApp(LinkItem item)
    {
        var exe = item.Exe ?? item.Target;
        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        // Z 順序最上面的就是最近用過的那個視窗
        var window = WindowFinder.List().FirstOrDefault(w => WindowFinder.SamePath(w.ExePath, exe) && w.FolderPath is null);
        return window is not null && WindowFinder.Activate(window.Handle);
    }

    private static bool SwitchToFolder(string folder)
    {
        foreach (var (hwnd, path) in WindowFinder.ExplorerFolders())
        {
            try
            {
                if (WindowFinder.SamePath(path, folder)) return WindowFinder.Activate(hwnd);
            }
            catch { /* 特殊路徑(例如「本機」)比對失敗就略過 */ }
        }
        return false;
    }

    private static void Launch(LinkItem item)
    {
        var psi = item.Kind == LinkKind.Folder || item.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("explorer.exe", $"\"{item.Target}\"")
            : new ProcessStartInfo(item.Target) { UseShellExecute = true };
        if (item.Kind is LinkKind.App or LinkKind.File && File.Exists(item.Target))
            psi.WorkingDirectory = Path.GetDirectoryName(item.Target) ?? "";
        Process.Start(psi);
    }

    // ── 加入 / 移除 / 排序 ───────────────────────────────

    /// <summary>從路徑加入(程式、捷徑、資料夾、一般檔案);已經有就不重複。</summary>
    public LinkItem? AddPath(string path)
    {
        var item = FromPath(path);
        if (item is null) return null;
        return AddItem(item);
    }

    public LinkItem? AddUrl(string url)
    {
        url = url.Trim();
        if (!url.Contains("://")) url = "https://" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return AddItem(new LinkItem { Kind = LinkKind.Url, Name = uri.Host.Replace("www.", ""), Target = uri.ToString() });
    }

    public LinkItem? AddWindow(AppWindow window)
    {
        if (window.FolderPath is not null) return AddPath(window.FolderPath);
        var item = FromPath(window.ExePath);
        if (item is null) return null;
        // Microsoft Store 程式:記下應用程式識別碼,開啟時用 shell:AppsFolder 啟動(不能直接執行 WindowsApps 裡的 exe)
        if (window.ExePath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
            && WindowFinder.PackagedAppId(window.Handle) is { } aumid)
            item.Target = @"shell:AppsFolder\" + aumid;
        return AddItem(item);
    }

    private LinkItem? AddItem(LinkItem item)
    {
        if (Items.Any(i => i.Kind == item.Kind && string.Equals(i.Target, item.Target, StringComparison.OrdinalIgnoreCase))) return null;
        item.Icon = IconFor(item);
        Items.Add(item);
        Save();
        return item;
    }

    public void Remove(LinkItem item)
    {
        if (Items.Remove(item)) Save();
    }

    public void Move(LinkItem item, int delta)
    {
        int i = Items.IndexOf(item), j = i + delta;
        if (i < 0 || j < 0 || j >= Items.Count) return;
        Items.Move(i, j);
        Save();
    }

    private static LinkItem? FromPath(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path))
                return new LinkItem { Kind = LinkKind.Folder, Name = FolderName(path), Target = path };
            if (!File.Exists(path)) return null;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".exe":
                    return new LinkItem { Kind = LinkKind.App, Name = AppName(path), Target = path, Exe = path };
                case ".lnk":
                    var target = ShortcutTarget(path);
                    if (target is not null && Directory.Exists(target))
                        return new LinkItem { Kind = LinkKind.Folder, Name = Path.GetFileNameWithoutExtension(path), Target = target };
                    return new LinkItem
                    {
                        Kind = LinkKind.Shortcut,
                        Name = Path.GetFileNameWithoutExtension(path),
                        Target = path,
                        Exe = target is not null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : null,
                    };
                case ".url":
                    var url = File.ReadLines(path).FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..];
                    return url is null ? null : new LinkItem { Kind = LinkKind.Url, Name = Path.GetFileNameWithoutExtension(path), Target = url };
                default:
                    return new LinkItem { Kind = LinkKind.File, Name = Path.GetFileNameWithoutExtension(path), Target = path };
            }
        }
        catch
        {
            return null;
        }
    }

    private static string AppName(string exe)
    {
        try
        {
            var desc = FileVersionInfo.GetVersionInfo(exe).FileDescription;
            if (!string.IsNullOrWhiteSpace(desc) && desc.Length <= 24) return desc.Trim();
        }
        catch { }
        return Path.GetFileNameWithoutExtension(exe);
    }

    private static string FolderName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\'));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary>解析 .lnk 的目標(WScript.Shell);解析不了回傳 null。</summary>
    private static string? ShortcutTarget(string lnk)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return null;
            dynamic shell = Activator.CreateInstance(type)!;
            try
            {
                dynamic sc = shell.CreateShortcut(lnk);
                string target = sc.TargetPath;
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? IconFor(LinkItem item) => item.Kind switch
    {
        LinkKind.Url => LinkIcons.ForExtension(".url"),
        LinkKind.Shortcut or LinkKind.App when item.Exe is not null && File.Exists(item.Exe) => LinkIcons.Get(item.Exe),
        _ => LinkIcons.Get(item.Target),
    };

    // ── 拖放:在「捷徑」分頁開著時,把程式 / 捷徑 / 資料夾 / 網址拖到島上 ──

    public bool CanAccept(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.UnicodeText);

    public void Accept(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var p in paths) AddPath(p);
            return;
        }
        if (data.GetData(DataFormats.UnicodeText) is string text && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            AddUrl(uri.ToString());
    }

    // ── 存檔 ─────────────────────────────────────────────

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var list = JsonSerializer.Deserialize<List<LinkItem>>(File.ReadAllText(_file), JsonOptions) ?? [];
            foreach (var item in list)
            {
                item.Icon = IconFor(item);
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Host.Log($"捷徑:讀取 links.json 失敗:{ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(Items.ToList(), JsonOptions));
        }
        catch (Exception ex)
        {
            Host.Log($"捷徑:儲存失敗:{ex.Message}");
        }
    }
}

internal sealed class LinksOptions
{
    public bool WindowStrip { get; set; } = true;
}

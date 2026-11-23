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
public sealed class LinksModule : IslandModuleBase, IIslandDropTarget, IIslandSettingsPage, IIslandCompact
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
        Load();
    }

    public override FrameworkElement CreateExpandedView() => _view ??= new LinksView(this);
    public FrameworkElement CreateSettingsView() => new LinksSettingsView(this);

    // ── 小圓:滑鼠移到島上時固定顯示的捷徑入口,點一下打開捷徑分頁 ──
    private LinksCompactView? _compact;
    public bool IsCompactRelevant => Items.Count > 0;
    public bool ShowWithoutActivity => true;
    public FrameworkElement CreateCompactView() => _compact ??= new LinksCompactView(this);

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

using System.IO;
using System.Runtime.InteropServices;

namespace Island.Modules.Shelf;

/// <summary>
/// 監看 Windows 的「螢幕擷取畫面」資料夾(Win+Shift+S / Print Screen 自動儲存的位置,
/// 也就是 Known Folder「Screenshots」,OneDrive 備份時會在 OneDrive\圖片\Screenshots)。
/// 有新的圖片檔出現時通知(背景執行緒)。FileSystemWatcher 是事件驅動,平常不耗資源。
/// </summary>
internal sealed class ScreenshotWatcher : IDisposable
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg"];
    /// <summary>只收「剛剛」建立的檔案,避免 OneDrive 從別台電腦同步下來的舊截圖也被加進來。</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(20);

    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public string? Folder { get; private set; }

    /// <summary>新截圖的完整路徑(背景執行緒)。</summary>
    public event Action<string>? Captured;

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public bool Start()
    {
        Stop();
        Folder = FindFolder();
        if (Folder is null) return false;
        try
        {
            _watcher = new FileSystemWatcher(Folder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName,
                InternalBufferSize = 8192,
            };
            _watcher.Created += (_, e) => OnFile(e.FullPath);
            _watcher.Renamed += (_, e) => OnFile(e.FullPath);   // 有些工具先寫暫存檔再改名
            _watcher.EnableRaisingEvents = true;
            return true;
        }
        catch
        {
            _watcher?.Dispose();
            _watcher = null;
            return false;
        }
    }

    private void OnFile(string path)
    {
        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return;
        lock (_seen)
        {
            if (!_seen.Add(path)) return;
        }
        // 截圖工具可能還在寫檔:稍等再確認檔案可讀、而且是剛建立的
        ThreadPool.QueueUserWorkItem(_ =>
        {
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(300);
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists) return;
                    if (DateTime.Now - info.CreationTime > MaxAge) return;
                    using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
                    if (info.Length == 0) continue;
                    Captured?.Invoke(path);
                    return;
                }
                catch (IOException) { /* 還在寫,再等一下 */ }
                catch { return; }
            }
        });
    }

    /// <summary>Known Folder「Screenshots」;找不到就用「圖片\Screenshots」。</summary>
    private static string? FindFolder()
    {
        try
        {
            var id = new Guid("B7BC2E3B-E1E8-4B48-A0E2-08B16A2BF9C1");
            if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var ptr) == 0)
            {
                var path = Marshal.PtrToStringUni(ptr);
                Marshal.FreeCoTaskMem(ptr);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
            }
        }
        catch { }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        return Directory.Exists(fallback) ? fallback : null;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}

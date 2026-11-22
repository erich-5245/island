using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Island.Modules.Links;

/// <summary>一個開著的應用程式視窗。</summary>
public sealed record AppWindow(IntPtr Handle, string Title, string ExePath, string? FolderPath);

/// <summary>
/// 找「開著的應用程式視窗」並把它切到前景。只在使用者點擊或打開設定頁時呼叫,平常不做任何事。
/// - 一般視窗:可見、沒有擁有者、不是工具視窗、沒被 DWM 隱藏、有標題。
/// - 檔案總管視窗另外用 Shell.Application 查出它正在顯示的資料夾。
/// </summary>
internal static class WindowFinder
{
    /// <summary>依 Z 順序(最上面 = 最近用過)列出所有應用程式視窗。</summary>
    public static List<AppWindow> List()
    {
        var folders = ExplorerFolders();
        var result = new List<AppWindow>();
        var title = new StringBuilder(512);
        EnumWindows((h, _) =>
        {
            if (!IsAppWindow(h)) return true;
            title.Clear();
            GetWindowText(h, title, title.Capacity);
            if (title.Length == 0) return true;
            var exe = ProcessPath(h);
            if (exe is null) return true;
            folders.TryGetValue(h, out var folder);
            result.Add(new AppWindow(h, title.ToString(), exe, folder));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsAppWindow(IntPtr h)
    {
        if (!IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero) return false;
        long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        if ((ex & WS_EX_TOOLWINDOW) != 0) return false;
        if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        return true;
    }

    public static string? ProcessPath(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Microsoft Store 程式(放在 WindowsApps 的封裝程式)的應用程式識別碼(AUMID)。
    /// 這類程式不能直接執行 exe,要用 shell:AppsFolder\{AUMID} 啟動。不是封裝程式回傳 null。
    /// </summary>
    public static string? PackagedAppId(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            int len = 512;
            var sb = new StringBuilder(len);
            return GetApplicationUserModelId(handle, ref len, sb) == 0 ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>檔案總管視窗 → 正在顯示的資料夾(Shell.Application,COM)。</summary>
    public static Dictionary<IntPtr, string> ExplorerFolders()
    {
        var map = new Dictionary<IntPtr, string>();
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return map;
            dynamic shell = Activator.CreateInstance(type)!;
            try
            {
                foreach (dynamic w in shell.Windows())
                {
                    try
                    {
                        string? path = w.Document?.Folder?.Self?.Path;
                        if (string.IsNullOrEmpty(path)) continue;
                        map[new IntPtr((long)w.HWND)] = path!;
                    }
                    catch { /* 不是檔案總管(例如 IE 殘留)就略過 */ }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
        catch { }
        return map;
    }

    /// <summary>把視窗切到前景;最小化的先還原。</summary>
    public static bool Activate(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (SetForegroundWindow(hwnd)) return true;

        // 前景鎖定時的常見做法:送一個 Alt 鍵的「按下放開」,系統就允許換前景
        keybd_event(VK_MENU, 0, 0, IntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        return SetForegroundWindow(hwnd);
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    // ── Win32 ─────────────────────────────────────────────

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const int SW_RESTORE = 9;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 2;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process, ref int length, StringBuilder id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);
}

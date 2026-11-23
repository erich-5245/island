using System.Runtime.InteropServices;

namespace Island.App.Interop;

/// <summary>
/// Windows 工具列:自動隱藏(和 Windows 設定裡「自動隱藏工作列」是同一個設定)、打開開始功能表。
/// 只在使用者按開關 / 按鈕時呼叫。
/// </summary>
internal static class Taskbar
{
    private const uint ABM_GETSTATE = 0x4, ABM_SETSTATE = 0xA;
    private const int ABS_AUTOHIDE = 0x1, ABS_ALWAYSONTOP = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    private static APPBARDATA Data() => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = FindWindow("Shell_TrayWnd", null),
    };

    public static bool IsAutoHide
    {
        get
        {
            var d = Data();
            return ((long)SHAppBarMessage(ABM_GETSTATE, ref d).ToUInt64() & ABS_AUTOHIDE) != 0;
        }
    }

    /// <summary>開 / 關自動隱藏;其他狀態(例如永遠在最上層)保留原樣。</summary>
    public static void SetAutoHide(bool on)
    {
        var d = Data();
        long state = (long)SHAppBarMessage(ABM_GETSTATE, ref d).ToUInt64();
        state = on ? state | ABS_AUTOHIDE : state & ~ABS_AUTOHIDE;
        d.lParam = (IntPtr)(state | ABS_ALWAYSONTOP);
        SHAppBarMessage(ABM_SETSTATE, ref d);
    }

    /// <summary>打開開始功能表(和按一下 Windows 鍵一樣)。</summary>
    public static void OpenStart()
    {
        const byte VK_LWIN = 0x5B;
        const uint KEYEVENTF_KEYUP = 0x2;
        keybd_event(VK_LWIN, 0, 0, IntPtr.Zero);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }
}

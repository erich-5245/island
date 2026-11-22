using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Island.App.Interop;

/// <summary>
/// 全域快捷鍵(RegisterHotKey)。按下時系統送 WM_HOTKEY 給島的視窗,不需要鍵盤掛勾、不耗資源。
/// 字串格式:「Ctrl+Alt+I」、「Win+Shift+I」;空字串 = 停用。
/// </summary>
internal static class GlobalHotkey
{
    public const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>註冊;字串無效或已被其他程式佔用時回傳 false。</summary>
    public static bool Register(IntPtr hwnd, int id, string? text)
    {
        UnregisterHotKey(hwnd, id);
        if (!TryParse(text, out var mods, out var vk)) return false;
        return RegisterHotKey(hwnd, id, mods | MOD_NOREPEAT, vk);
    }

    /// <summary>註冊單一按鍵(例如展開時的 Esc)。</summary>
    public static bool RegisterKey(IntPtr hwnd, int id, Key key)
    {
        UnregisterHotKey(hwnd, id);
        return RegisterHotKey(hwnd, id, MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public static void Unregister(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);

    public static bool TryParse(string? text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        Key? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win": case "windows": modifiers |= MOD_WIN; break;
                default:
                    if (Enum.TryParse<Key>(raw, ignoreCase: true, out var k)) key = k;
                    else return false;
                    break;
            }
        }
        if (key is null || modifiers == 0) return false;   // 至少要一個修飾鍵,避免搶走一般按鍵
        vk = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        return vk != 0;
    }

    /// <summary>把按下的組合轉成字串(設定視窗錄製快捷鍵用);只有修飾鍵時回傳 null。</summary>
    public static string? Format(ModifierKeys modifiers, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.None) return null;
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (parts.Count == 0) return null;
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}

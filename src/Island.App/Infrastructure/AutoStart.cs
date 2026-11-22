using Microsoft.Win32;

namespace Island.App.Infrastructure;

/// <summary>
/// 開機(登入)自動啟動。寫在 HKCU\...\Run,只影響目前使用者,
/// 不需要系統管理員權限,啟動時也是一般權限(拖放才正常)。
/// </summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Island";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
        Log.Write($"開機自動啟動:{(enabled ? "開啟 " + Command : "關閉")}");
    }

    /// <summary>已開啟但程式被移到別的位置(例如重新建置到不同資料夾)時,更新成目前的路徑。</summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current
                && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, Command);
                Log.Write($"開機自動啟動路徑更新:{current} → {Command}");
            }
        }
        catch (Exception ex)
        {
            Log.Write($"更新開機自動啟動路徑失敗:{ex.Message}");
        }
    }
}

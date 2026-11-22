using System.IO;

namespace Island.App.Infrastructure;

/// <summary>極簡檔案 log。只在事件發生時寫入,閒置時不做任何事。</summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var info = new FileInfo(AppPaths.LogFile);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(AppPaths.LogFile, AppPaths.LogFile + ".old", overwrite: true);
                File.AppendAllText(AppPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // log 失敗不能讓程式掛掉
        }
    }
}

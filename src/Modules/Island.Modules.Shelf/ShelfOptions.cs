using System.IO;
using System.Text.Json;

namespace Island.Modules.Shelf;

/// <summary>%LOCALAPPDATA%\Island\modules\shelf\options.json</summary>
internal sealed class ShelfOptions
{
    /// <summary>截圖(Win+Shift+S / Print Screen 自動存檔)時自動放進 Shelf 並提示。</summary>
    public bool CaptureScreenshots { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ShelfOptions Load(string directory)
    {
        var file = Path.Combine(directory, "options.json");
        try
        {
            if (File.Exists(file))
                return JsonSerializer.Deserialize<ShelfOptions>(File.ReadAllText(file), JsonOptions) ?? new ShelfOptions();
            Directory.CreateDirectory(directory);
            File.WriteAllText(file, JsonSerializer.Serialize(new ShelfOptions(), JsonOptions));
        }
        catch { /* 讀不到就用預設 */ }
        return new ShelfOptions();
    }
}

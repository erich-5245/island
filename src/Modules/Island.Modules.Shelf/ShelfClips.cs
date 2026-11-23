using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Island.Modules.Shelf;

/// <summary>Shelf 收文字 / 網址 / 圖片用的純邏輯(不碰 UI,方便測試)。</summary>
internal static partial class ShelfClips
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];
    private static readonly string[] UrlSchemes = ["http", "https", "ftp", "mailto"];

    public const int MaxTitleLength = 24;

    public static bool IsImageName(string name) =>
        ImageExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    /// <summary>單行、可以解析成 http(s)/ftp/mailto(或 www. 開頭)→ 網址;其他 → 文字。</summary>
    public static (ShelfKind Kind, string Value) ClassifyText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Contains('\n') || trimmed.Contains(' ')) return (ShelfKind.Text, text);

        var candidate = trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + trimmed : trimmed;
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            UrlSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase) &&
            (uri.Scheme == "mailto" || uri.Host.Contains('.') || uri.IsLoopback))
            return (ShelfKind.Url, candidate);
        return (ShelfKind.Text, text);
    }

    /// <summary>文字項目的標題:第一個非空白行,空白合併,太長截斷。</summary>
    public static string TextTitle(string text)
    {
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        line = Spaces().Replace(line, " ");
        if (line.Length == 0) return "(空白文字)";
        return line.Length > MaxTitleLength ? line[..(MaxTitleLength - 1)] + "…" : line;
    }

    /// <summary>網址項目的標題:主機名稱(去掉 www.);mailto 顯示信箱。</summary>
    public static string UrlTitle(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return TextTitle(url);
        if (uri.Scheme == "mailto") return TextTitle(uri.OriginalString["mailto:".Length..]);
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        return host.Length == 0 ? TextTitle(url) : host;
    }

    /// <summary>
    /// 從 FILEGROUPDESCRIPTORW 讀出第一個虛擬檔案的檔名(瀏覽器拖圖片、Outlook 拖附件時會用這個格式)。
    /// 結構:UINT cItems,接著 FILEDESCRIPTORW[],檔名在每個描述的第 72 位元組,長度 260 個 UTF-16 字元。
    /// </summary>
    public static string? ReadFileGroupName(Stream stream)
    {
        const int NameOffset = 4 + 72;
        const int NameChars = 260;
        try
        {
            stream.Position = 0;
            var buf = new byte[NameOffset + NameChars * 2];
            int read = 0, n;
            while (read < buf.Length && (n = stream.Read(buf, read, buf.Length - read)) > 0) read += n;
            if (read < 4 || BitConverter.ToUInt32(buf, 0) == 0 || read < NameOffset + 2) return null;
            var name = Encoding.Unicode.GetString(buf, NameOffset, Math.Min(NameChars * 2, read - NameOffset));
            int end = name.IndexOf('\0');
            if (end >= 0) name = name[..end];
            return SafeFileName(name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>去掉路徑與不合法字元;空的就回 null。</summary>
    public static string? SafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = Path.GetFileName(name.Replace('/', '\\'));
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        return name.Length == 0 ? null : name;
    }

    /// <summary>在資料夾裡找一個不重複的檔名(name.png → name (2).png …)。</summary>
    public static string UniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            path = Path.Combine(directory, $"{stem} ({i}){ext}");
            if (!File.Exists(path)) return path;
        }
    }

    public static bool IsInside(string path, string directory)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var dir = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
            return full.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ── shelf.json ───────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(IEnumerable<ShelfRecord> records) => JsonSerializer.Serialize(records.ToList(), JsonOptions);

    /// <summary>讀 shelf.json。舊版只存路徑字串陣列,一樣讀得懂。</summary>
    public static List<ShelfRecord> Deserialize(string json)
    {
        var result = new List<ShelfRecord>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.String)
            {
                if (e.GetString() is { Length: > 0 } p) result.Add(new ShelfRecord { Kind = ShelfKind.File, Path = p });
            }
            else if (e.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    if (e.Deserialize<ShelfRecord>(JsonOptions) is { } r && IsValid(r)) result.Add(r);
                }
                catch (JsonException) { /* 壞掉的一筆略過 */ }
            }
        }
        return result;
    }

    private static bool IsValid(ShelfRecord r) => r.Kind is ShelfKind.Text or ShelfKind.Url
        ? !string.IsNullOrEmpty(r.Text)
        : !string.IsNullOrEmpty(r.Path);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

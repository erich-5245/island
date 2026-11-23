using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island.Modules.Shelf;

public enum ShelfKind { File, Text, Url, Image }

/// <summary>
/// Shelf 上的一個項目。
/// - File:只保存路徑,不複製檔案。
/// - Text / Url:文字直接存在 shelf.json。
/// - Image:拖進來的圖片(或瀏覽器拖出的虛擬檔案)存成 modules\shelf\clips 裡的檔案,移除時一併刪除。
/// </summary>
public sealed class ShelfItem
{
    private ShelfItem(ShelfKind kind, string? path, string? text, bool owned)
    {
        Kind = kind;
        Path = path ?? "";
        Text = text ?? "";
        IsOwned = owned;
        switch (kind)
        {
            case ShelfKind.Text:
                Name = ShelfClips.TextTitle(Text);
                Glyph = "";   // Aa
                Tip = Text.Length > 400 ? Text[..400] + "…" : Text;
                break;
            case ShelfKind.Url:
                Name = ShelfClips.UrlTitle(Text);
                Glyph = "";   // 連結
                Tip = Text;
                break;
            case ShelfKind.Image:
                Name = System.IO.Path.GetFileName(Path);
                Icon = Thumbnail(Path) ?? ShellIcons.Get(Path);
                Tip = Path;
                break;
            default:
                var trimmed = Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                Name = System.IO.Path.GetFileName(trimmed) is { Length: > 0 } n ? n : Path;
                Icon = ShellIcons.Get(Path);
                Tip = Path;
                break;
        }
    }

    public static ShelfItem FromPath(string path, bool owned = false) =>
        new(owned && ShelfClips.IsImageName(path) ? ShelfKind.Image : ShelfKind.File, path, null, owned);

    public static ShelfItem FromText(string text)
    {
        var (kind, value) = ShelfClips.ClassifyText(text);
        return new ShelfItem(kind, null, value, false);
    }

    internal static ShelfItem FromRecord(ShelfRecord r) => r.Kind switch
    {
        ShelfKind.Text or ShelfKind.Url => new ShelfItem(r.Kind, null, r.Text, false),
        _ => new ShelfItem(r.Kind, r.Path, null, r.Owned == true),
    };

    internal ShelfRecord ToRecord() => Kind is ShelfKind.Text or ShelfKind.Url
        ? new ShelfRecord { Kind = Kind, Text = Text }
        : new ShelfRecord { Kind = Kind, Path = Path, Owned = IsOwned ? true : null };

    public ShelfKind Kind { get; }
    /// <summary>File / Image 的路徑。</summary>
    public string Path { get; }
    /// <summary>Text / Url 的內容。</summary>
    public string Text { get; }
    /// <summary>檔案是 Shelf 自己存的(在 clips 資料夾),移除時刪掉、拖出時只複製。</summary>
    public bool IsOwned { get; }

    public string Name { get; } = "";
    public ImageSource? Icon { get; }
    /// <summary>沒有圖示時顯示的字型圖示。</summary>
    public string? Glyph { get; }
    public string Tip { get; } = "";

    public bool IsFileBased => Kind is ShelfKind.File or ShelfKind.Image;
    public bool HasIcon => Icon is not null;

    public bool Exists => !IsFileBased || File.Exists(Path) || Directory.Exists(Path);

    /// <summary>同一個項目(用來避免重複加入)。</summary>
    public bool SameAs(ShelfItem other) =>
        Kind == other.Kind && (IsFileBased
            ? string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
            : string.Equals(Text, other.Text, StringComparison.Ordinal));

    private static ImageSource? Thumbnail(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // 讀完就關檔,之後可以刪除
            bmp.DecodePixelWidth = 56;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>shelf.json 的一筆紀錄。</summary>
internal sealed class ShelfRecord
{
    public ShelfKind Kind { get; set; }
    public string? Path { get; set; }
    public string? Text { get; set; }
    public bool? Owned { get; set; }
}

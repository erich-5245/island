using System.IO;
using System.Windows.Media;

namespace Island.Modules.Shelf;

/// <summary>Shelf 上的一個項目:只保存「路徑」,不複製檔案。</summary>
public sealed class ShelfItem
{
    public ShelfItem(string path)
    {
        Path = path;
        var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        Name = System.IO.Path.GetFileName(trimmed) is { Length: > 0 } n ? n : path;
        Icon = ShellIcons.Get(path);
    }

    public string Path { get; }
    public string Name { get; }
    public ImageSource? Icon { get; }

    public bool Exists => File.Exists(Path) || Directory.Exists(Path);
}

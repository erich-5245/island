using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Island.Core;
using Island.Modules.Shelf;

namespace Island.Tests;

/// <summary>假的外殼:資料放在暫存資料夾,其他呼叫只記錄下來。</summary>
internal sealed class FakeHost : IIslandHost, IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("island-host-").FullName;
    public List<string> Logs { get; } = [];
    public Dispatcher Dispatcher { get; } = Dispatcher.CurrentDispatcher;

    public string GetModuleDataDirectory(string moduleId)
    {
        var dir = Path.Combine(Root, "modules", moduleId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Log(string message) => Logs.Add(message);
    public void Expand(string moduleId) { }
    public void Collapse() { }
    public IDisposable HoldExpanded() => this;
    public void Announce(string moduleId, TimeSpan duration) { }
    public void SetAccent(Color? color) { }
    public bool SendToModule(string moduleId, IDataObject data) => false;

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}

/// <summary>Shelf 模組的接收 / 保存 / 移除流程(用假的拖曳資料,不需要滑鼠)。</summary>
public sealed class ShelfModuleTests : IDisposable
{
    private readonly FakeHost _host = new();

    private ShelfModule NewShelf()
    {
        // 測試時不要監看真正的截圖資料夾
        File.WriteAllText(Path.Combine(_host.GetModuleDataDirectory("shelf"), "options.json"), """{ "captureScreenshots": false }""");
        var shelf = new ShelfModule();
        shelf.Initialize(_host);
        return shelf;
    }

    private string ShelfJson => File.ReadAllText(Path.Combine(_host.GetModuleDataDirectory("shelf"), "shelf.json"));
    private string ClipsDir => Path.Combine(_host.GetModuleDataDirectory("shelf"), "clips");

    private static DataObject Text(string s) => new(DataFormats.UnicodeText, s);

    private static BitmapSource RedSquare()
    {
        var pixels = new byte[16 * 16 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = 255; pixels[i + 3] = 255; }
        var bmp = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 64);
        bmp.Freeze();
        return bmp;
    }

    [Fact]
    public void AcceptsText_AndUrl_AndSkipsDuplicates()
    {
        var shelf = NewShelf();
        Assert.True(shelf.CanAccept(Text("hi")));
        shelf.Accept(Text("買牛奶"));
        shelf.Accept(Text("https://example.com"));
        shelf.Accept(Text("https://example.com"));
        Assert.Equal(2, shelf.Items.Count);
        Assert.Equal(ShelfKind.Text, shelf.Items[0].Kind);
        Assert.Equal(ShelfKind.Url, shelf.Items[1].Kind);
        Assert.Contains("買牛奶", ShelfJson);
        Assert.Equal("Shelf 2", shelf.Badge?.Text);
    }

    [Fact]
    public void AcceptsBitmap_AsOwnedPng_AndDeletesOnRemove()
    {
        var shelf = NewShelf();
        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, RedSquare());
        Assert.True(shelf.CanAccept(data));
        shelf.Accept(data);

        var item = Assert.Single(shelf.Items);
        Assert.Equal(ShelfKind.Image, item.Kind);
        Assert.True(item.IsOwned);
        Assert.True(File.Exists(item.Path));
        Assert.StartsWith(ClipsDir, item.Path);
        Assert.NotNull(item.Icon);           // 縮圖

        shelf.Remove(item);
        Assert.Empty(shelf.Items);
        Assert.False(File.Exists(item.Path));
    }

    [Fact]
    public void AcceptsVirtualFile_FromBrowser()
    {
        var shelf = NewShelf();
        var descriptor = new byte[4 + 592];
        BitConverter.GetBytes(1u).CopyTo(descriptor, 0);
        Encoding.Unicode.GetBytes("cat.png").CopyTo(descriptor, 76);
        var png = new MemoryStream();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(RedSquare()));
        enc.Save(png);

        var data = new DataObject();
        data.SetData("FileGroupDescriptorW", new MemoryStream(descriptor));
        data.SetData("FileContents", png);
        data.SetData(DataFormats.UnicodeText, "https://example.com/cat.png");   // 瀏覽器也會附網址:應該用檔案
        shelf.Accept(data);

        var item = Assert.Single(shelf.Items);
        Assert.Equal(ShelfKind.Image, item.Kind);
        Assert.Equal("cat.png", item.Name);
        Assert.Equal(png.Length, new FileInfo(item.Path).Length);
    }

    [Fact]
    public void FilesWin_OverText()
    {
        var shelf = NewShelf();
        var file = Path.Combine(_host.Root, "doc.txt");
        File.WriteAllText(file, "x");
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { file });
        data.SetData(DataFormats.UnicodeText, file);
        shelf.Accept(data);

        var item = Assert.Single(shelf.Items);
        Assert.Equal(ShelfKind.File, item.Kind);
        Assert.False(item.IsOwned);

        shelf.Clear();
        Assert.True(File.Exists(file));      // 使用者自己的檔案不會被刪
    }

    [Fact]
    public void Clear_DeletesOnlyOwnedClips()
    {
        var shelf = NewShelf();
        var bmp = new DataObject();
        bmp.SetData(DataFormats.Bitmap, RedSquare());
        shelf.Accept(bmp);
        shelf.Accept(Text("note"));
        var clip = shelf.Items[0].Path;

        shelf.Clear();
        Assert.Empty(shelf.Items);
        Assert.False(File.Exists(clip));
        Assert.Equal("[]", ShelfJson.Trim());
    }

    [Fact]
    public void Reload_KeepsAllKinds_AndReadsOldFormat()
    {
        var dir = _host.GetModuleDataDirectory("shelf");
        var file = Path.Combine(_host.Root, "old.txt");
        File.WriteAllText(file, "x");
        // 舊版 shelf.json:只有路徑字串;不存在的路徑啟動時會被清掉
        File.WriteAllText(Path.Combine(dir, "shelf.json"),
            $"[{System.Text.Json.JsonSerializer.Serialize(file)}, \"C:\\\\does-not-exist\\\\gone.txt\"]");

        var shelf = NewShelf();
        var only = Assert.Single(shelf.Items);
        Assert.Equal(file, only.Path);

        shelf.Accept(Text("記得帶傘"));
        shelf.Accept(Text("www.cwa.gov.tw"));

        var again = NewShelf();
        Assert.Equal(new[] { ShelfKind.File, ShelfKind.Text, ShelfKind.Url }, again.Items.Select(i => i.Kind));
        Assert.Equal("https://www.cwa.gov.tw", again.Items[2].Text);
    }

    [Fact]
    public void IgnoresEmptyAndUnknownData()
    {
        var shelf = NewShelf();
        Assert.False(shelf.CanAccept(new DataObject("SomeCustomFormat", 42)));
        shelf.Accept(Text(""));
        var bad = new DataObject();
        bad.SetData("FileGroupDescriptorW", new MemoryStream([0, 0, 0, 0]));
        shelf.Accept(bad);
        Assert.Empty(shelf.Items);
    }

    public void Dispose() => _host.Dispose();
}

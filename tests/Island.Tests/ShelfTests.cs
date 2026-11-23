using System.IO;
using System.Text;
using Island.Modules.Shelf;

namespace Island.Tests;

public class ShelfClipsTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", ShelfKind.Url, "https://www.youtube.com/watch?v=abc")]
    [InlineData("  http://example.com/a  ", ShelfKind.Url, "http://example.com/a")]
    [InlineData("www.google.com", ShelfKind.Url, "https://www.google.com")]
    [InlineData("mailto:someone@example.com", ShelfKind.Url, "mailto:someone@example.com")]
    [InlineData("http://localhost:5173", ShelfKind.Url, "http://localhost:5173")]
    [InlineData("hello world", ShelfKind.Text, "hello world")]
    [InlineData("notes.txt", ShelfKind.Text, "notes.txt")]
    [InlineData("localhost:3000", ShelfKind.Text, "localhost:3000")]
    [InlineData("C:\\Users\\a.txt", ShelfKind.Text, "C:\\Users\\a.txt")]
    [InlineData("https://a.com\nhttps://b.com", ShelfKind.Text, "https://a.com\nhttps://b.com")]
    [InlineData("see https://a.com", ShelfKind.Text, "see https://a.com")]
    public void ClassifyText(string input, ShelfKind kind, string value)
    {
        var (k, v) = ShelfClips.ClassifyText(input);
        Assert.Equal(kind, k);
        Assert.Equal(value, v);
    }

    [Fact]
    public void TextTitle_UsesFirstNonEmptyLine_AndCollapsesSpaces()
    {
        Assert.Equal("第一行 內容", ShelfClips.TextTitle("\n\n  第一行   內容 \n第二行"));
    }

    [Fact]
    public void TextTitle_TruncatesLongText()
    {
        var title = ShelfClips.TextTitle(new string('a', 100));
        Assert.Equal(ShelfClips.MaxTitleLength, title.Length);
        Assert.EndsWith("…", title);
    }

    [Fact]
    public void TextTitle_Blank() => Assert.Equal("(空白文字)", ShelfClips.TextTitle("   \n  "));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", "youtube.com")]
    [InlineData("https://docs.github.com/en", "docs.github.com")]
    [InlineData("mailto:someone@example.com", "someone@example.com")]
    public void UrlTitle(string url, string expected) => Assert.Equal(expected, ShelfClips.UrlTitle(url));

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("A.JPG", true)]
    [InlineData("photo.webp", true)]
    [InlineData("doc.pdf", false)]
    [InlineData("noext", false)]
    public void IsImageName(string name, bool expected) => Assert.Equal(expected, ShelfClips.IsImageName(name));

    [Theory]
    [InlineData("cat.png", "cat.png")]
    [InlineData("..\\..\\evil.png", "evil.png")]
    [InlineData("dir/sub/x.jpg", "x.jpg")]
    [InlineData("a*b?.png", "a_b_.png")]
    [InlineData("   ", null)]
    [InlineData("...", null)]
    public void SafeFileName(string input, string? expected) => Assert.Equal(expected, ShelfClips.SafeFileName(input));

    /// <summary>組一個 FILEGROUPDESCRIPTORW:cItems + 一個 592 bytes 的 FILEDESCRIPTORW,檔名在第 72 byte。</summary>
    private static MemoryStream Descriptor(string name, uint count = 1)
    {
        var buf = new byte[4 + 592];
        BitConverter.GetBytes(count).CopyTo(buf, 0);
        Encoding.Unicode.GetBytes(name).CopyTo(buf, 4 + 72);
        return new MemoryStream(buf);
    }

    [Fact]
    public void ReadFileGroupName_ReadsFirstName() =>
        Assert.Equal("貓咪 照片.jpg", ShelfClips.ReadFileGroupName(Descriptor("貓咪 照片.jpg")));

    [Fact]
    public void ReadFileGroupName_StripsPath() =>
        Assert.Equal("x.png", ShelfClips.ReadFileGroupName(Descriptor("folder\\x.png")));

    [Fact]
    public void ReadFileGroupName_EmptyGroup() =>
        Assert.Null(ShelfClips.ReadFileGroupName(Descriptor("x.png", count: 0)));

    [Fact]
    public void ReadFileGroupName_Truncated() =>
        Assert.Null(ShelfClips.ReadFileGroupName(new MemoryStream([1, 0, 0, 0, 5])));

    [Fact]
    public void UniquePath_AddsNumber()
    {
        var dir = Directory.CreateTempSubdirectory("island-test-").FullName;
        try
        {
            Assert.Equal(Path.Combine(dir, "a.png"), ShelfClips.UniquePath(dir, "a.png"));
            File.WriteAllText(Path.Combine(dir, "a.png"), "");
            Assert.Equal(Path.Combine(dir, "a (2).png"), ShelfClips.UniquePath(dir, "a.png"));
            File.WriteAllText(Path.Combine(dir, "a (2).png"), "");
            Assert.Equal(Path.Combine(dir, "a (3).png"), ShelfClips.UniquePath(dir, "a.png"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"C:\data\clips\a.png", @"C:\data\clips", true)]
    [InlineData(@"C:\data\clips\sub\a.png", @"C:\data\clips\", true)]
    [InlineData(@"C:\data\clipsX\a.png", @"C:\data\clips", false)]
    [InlineData(@"C:\data\clips\..\a.png", @"C:\data\clips", false)]
    public void IsInside(string path, string dir, bool expected) => Assert.Equal(expected, ShelfClips.IsInside(path, dir));

    [Fact]
    public void Deserialize_ReadsOldFormat()
    {
        var records = ShelfClips.Deserialize("""["C:\\a.txt", "C:\\b.png", ""]""");
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(ShelfKind.File, r.Kind));
        Assert.Equal(@"C:\a.txt", records[0].Path);
    }

    [Fact]
    public void Serialize_RoundTrips_AllKinds()
    {
        var input = new List<ShelfRecord>
        {
            new() { Kind = ShelfKind.File, Path = @"C:\a.txt" },
            new() { Kind = ShelfKind.Image, Path = @"C:\clips\x.png", Owned = true },
            new() { Kind = ShelfKind.Text, Text = "多行\n文字 \"引號\"" },
            new() { Kind = ShelfKind.Url, Text = "https://example.com/?q=1&b=2" },
        };
        var json = ShelfClips.Serialize(input);
        Assert.Contains("\"kind\": \"url\"", json);
        Assert.Contains("多行", json);          // 中文不跳脫,方便手動查看
        Assert.Contains("&b=2", json);
        var output = ShelfClips.Deserialize(json);
        Assert.Equal(4, output.Count);
        for (int i = 0; i < input.Count; i++)
        {
            Assert.Equal(input[i].Kind, output[i].Kind);
            Assert.Equal(input[i].Path, output[i].Path);
            Assert.Equal(input[i].Text, output[i].Text);
            Assert.Equal(input[i].Owned, output[i].Owned);
        }
    }

    [Fact]
    public void Deserialize_SkipsBrokenEntries()
    {
        var records = ShelfClips.Deserialize("""[{"kind":"text"}, {"kind":"nonsense","path":"x"}, 5, {"kind":"url","text":"https://a.com"}]""");
        var only = Assert.Single(records);
        Assert.Equal(ShelfKind.Url, only.Kind);
    }
}

public class ShelfItemTests
{
    [Fact]
    public void FromText_Url()
    {
        var item = ShelfItem.FromText("https://www.example.com/page");
        Assert.Equal(ShelfKind.Url, item.Kind);
        Assert.Equal("example.com", item.Name);
        Assert.False(item.IsFileBased);
        Assert.True(item.Exists);
        Assert.NotNull(item.Glyph);
    }

    [Fact]
    public void FromText_Text()
    {
        var item = ShelfItem.FromText("買牛奶\n買雞蛋");
        Assert.Equal(ShelfKind.Text, item.Kind);
        Assert.Equal("買牛奶", item.Name);
        Assert.Equal("買牛奶\n買雞蛋", item.Text);
    }

    [Fact]
    public void SameAs_ComparesByKindAndContent()
    {
        Assert.True(ShelfItem.FromText("abc").SameAs(ShelfItem.FromText("abc")));
        Assert.False(ShelfItem.FromText("abc").SameAs(ShelfItem.FromText("ABC")));
        Assert.True(ShelfItem.FromPath(@"C:\X\a.txt").SameAs(ShelfItem.FromPath(@"c:\x\A.TXT")));
    }

    [Fact]
    public void OwnedImage_RecordRoundTrip()
    {
        var item = ShelfItem.FromPath(@"C:\nowhere\clip.png", owned: true);
        Assert.Equal(ShelfKind.Image, item.Kind);
        Assert.False(item.Exists);
        var back = ShelfItem.FromRecord(item.ToRecord());
        Assert.Equal(ShelfKind.Image, back.Kind);
        Assert.True(back.IsOwned);
    }

    [Fact]
    public void NotOwnedImageFile_IsPlainFile()
    {
        // 使用者自己的圖片檔:當一般檔案處理(拖出可以移動、移除時不刪檔)
        var item = ShelfItem.FromPath(@"C:\Pictures\me.png");
        Assert.Equal(ShelfKind.File, item.Kind);
        Assert.False(item.IsOwned);
        Assert.Null(item.ToRecord().Owned);
    }
}

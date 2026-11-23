using System.IO;
using Island.App.Infrastructure;

namespace Island.Tests;

public sealed class SettingsBundleTests : IDisposable
{
    private readonly string _src = Directory.CreateTempSubdirectory("island-src-").FullName;
    private readonly string _dst = Directory.CreateTempSubdirectory("island-dst-").FullName;

    private void Write(string root, string rel, string content)
    {
        var path = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void RoundTrip_SkipsPrivateAndMachineSpecific()
    {
        Write(_src, "settings.json", """{ "hotkey": "Ctrl+Alt+I", "theme": { "name": "深藍金" } }""");
        Write(_src, "modules/links/links.json", """[{ "name": "Chrome" }]""");
        Write(_src, "modules/todo/todos.json", """[{ "text": "買牛奶" }]""");
        Write(_src, "modules/shelf/shelf.json", """["C:\\a.txt"]""");                       // 路徑只在這台電腦有效
        Write(_src, "modules/shelf/options.json", """{ "captureScreenshots": false }""");
        Write(_src, "modules/calendar/options.json", """{ "icsUrl": "https://secret" }"""); // 私人網址
        Write(_src, "modules/shelf/clips/x.json", "{}");
        Write(_src, "island.log", "log");

        var json = SettingsBundle.Export(_src);
        Assert.Contains("深藍金", json);
        Assert.Contains("買牛奶", json);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("a.txt", json);
        Assert.DoesNotContain("island.log", json);

        int n = SettingsBundle.Import(_dst, json);
        Assert.Equal(4, n);   // settings + links + todo + shelf/options
        Assert.Contains("深藍金", File.ReadAllText(Path.Combine(_dst, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_dst, "modules", "todo", "todos.json")));
        Assert.False(File.Exists(Path.Combine(_dst, "modules", "calendar", "options.json")));
        Assert.False(File.Exists(Path.Combine(_dst, "modules", "shelf", "shelf.json")));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "format": "something-else", "files": {} }""")]
    [InlineData("""{ "format": "island-settings" }""")]
    [InlineData("[1,2]")]
    public void Import_RejectsNonBundles(string json)
    {
        Assert.Throws<InvalidDataException>(() => SettingsBundle.Import(_dst, json));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_dst));
    }

    [Fact]
    public void Import_CannotEscapeDataFolder()
    {
        var json = """
        {
          "format": "island-settings", "version": 1,
          "files": {
            "../evil.json": {},
            "modules/../../evil2.json": {},
            "C:/Windows/evil3.json": {},
            "modules/x/run.exe": {},
            "modules/ok/options.json": { "a": 1 }
          }
        }
        """;
        int n = SettingsBundle.Import(_dst, json);
        Assert.Equal(1, n);
        Assert.True(File.Exists(Path.Combine(_dst, "modules", "ok", "options.json")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_dst)!, "evil.json")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_src, true); } catch { }
        try { Directory.Delete(_dst, true); } catch { }
    }
}

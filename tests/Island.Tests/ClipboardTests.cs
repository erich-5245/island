using Island.Modules.Clipboard;

namespace Island.Tests;

public class ClipboardTests
{
    [Fact]
    public void NewestFirst_DuplicatesMoveUp_Capped()
    {
        var h = new ClipHistory();
        Assert.True(h.Add("a"));
        Assert.True(h.Add("b"));
        Assert.False(h.Add("b"));          // 和最上面一樣
        Assert.True(h.Add("a"));           // 重複 → 移到最上面
        Assert.Equal(new[] { "a", "b" }, h.Items);
        Assert.False(h.Add("   "));
        Assert.False(h.Add(null));
        for (int i = 0; i < 50; i++) h.Add($"x{i}");
        Assert.Equal(ClipHistory.MaxEntries, h.Items.Count);
        Assert.Equal("x49", h.Items[0]);
    }

    [Fact]
    public void LongText_Truncated()
    {
        var h = new ClipHistory();
        h.Add(new string('z', ClipHistory.MaxChars + 10));
        Assert.Equal(ClipHistory.MaxChars, h.Items[0].Length);
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("\n\n  第一行   有空白 \n第二行", "第一行 有空白…")]
    [InlineData("單行", "單行")]
    public void Preview(string text, string expected) => Assert.Equal(expected, ClipHistory.Preview(text));

    [Fact]
    public void Preview_Truncates() => Assert.Equal(40, ClipHistory.Preview(new string('a', 100)).Length);

    [Fact]
    public void Module_IsOffByDefault() =>
        Assert.False(((Island.Core.IIslandModule)new ClipboardModule()).EnabledByDefault);
}

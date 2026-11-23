using System.Windows;
using Island.App.Infrastructure;
using Island.Core;

namespace Island.Tests;

public class CircleActionTests
{
    private sealed class Fake(string? label, bool byDefault) : IIslandCompact
    {
        public bool IsCompactRelevant => true;
        public FrameworkElement? CreateCompactView() => null;
        public string? CompactActionLabel => label;
        public bool CompactActionByDefault => byDefault;
    }

    [Fact]
    public void NoLabel_AlwaysOpensTab()
    {
        var choices = new Dictionary<string, string> { ["x"] = CircleActions.Action };
        Assert.False(CircleActions.UseAction(choices, "x", new Fake(null, true)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NoChoice_UsesModuleDefault(bool byDefault, bool expected) =>
        Assert.Equal(expected, CircleActions.UseAction(new Dictionary<string, string>(), "x", new Fake("播放 / 暫停", byDefault)));

    [Fact]
    public void UserChoice_Wins_CaseInsensitive()
    {
        var m = new Fake("播放 / 暫停", byDefault: true);
        Assert.False(CircleActions.UseAction(new Dictionary<string, string> { ["MEDIA"] = CircleActions.Tab }, "media", m));
        Assert.True(CircleActions.UseAction(new Dictionary<string, string> { ["media"] = CircleActions.Action }, "Media", new Fake("x", false)));
    }

    [Fact]
    public void DefaultInterface_SingleCircle()
    {
        IIslandCompact m = new Fake("x", false);
        Assert.Equal(1, m.CompactCount);
        Assert.True(m.IsCompactRelevantAt(0));
        Assert.False(m.IsCompactRelevantAt(1));
        Assert.Null(m.CreateCompactView(1));
    }
}

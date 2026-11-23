using Island.Core;
using Island.Modules.Links;

namespace Island.Tests;

/// <summary>捷徑小圓:代表哪一個捷徑、換成別的、重新啟動後保留。</summary>
public sealed class LinksCircleTests : IDisposable
{
    private readonly FakeHost _host = new();

    private LinksModule NewLinks()
    {
        var m = new LinksModule();
        m.Initialize(_host);
        return m;
    }

    [Fact]
    public void NoLinks_NoCircle()
    {
        var links = NewLinks();
        Assert.Null(links.CircleItem);
        Assert.False(links.IsCompactRelevant);
        Assert.True(((IIslandCompact)links).ShowWithoutActivity);
    }

    [Fact]
    public void DefaultsToFirst_ThenFollowsStar_AndPersists()
    {
        var links = NewLinks();
        var a = links.AddUrl("https://a.example.com")!;
        var b = links.AddUrl("https://b.example.com")!;
        Assert.Same(a, links.CircleItem);           // 沒標星號 → 第一個

        links.SetCircle(b);
        Assert.Same(b, links.CircleItem);
        Assert.Single(links.Items, i => i.Circle);

        var again = NewLinks();                     // 重新啟動
        Assert.Equal(b.Target, again.CircleItem?.Target);

        again.Remove(again.CircleItem!);            // 移除小圓的捷徑 → 回到第一個
        Assert.Equal(a.Target, again.CircleItem?.Target);
    }

    [Fact]
    public void Stars_AtMostTwo_OldestDropped()
    {
        var links = NewLinks();
        var a = links.AddUrl("https://a.example.com")!;
        var b = links.AddUrl("https://b.example.com")!;
        var c = links.AddUrl("https://c.example.com")!;
        var compact = (IIslandCompact)links;
        Assert.Equal(2, compact.CompactCount);
        Assert.True(compact.IsCompactRelevantAt(0));
        Assert.False(compact.IsCompactRelevantAt(1));      // 沒標星號:只有第一個

        links.ToggleCircle(b);
        links.ToggleCircle(c);
        Assert.Equal(new[] { b, c }, links.CircleItems);
        Assert.True(compact.IsCompactRelevantAt(1));

        links.ToggleCircle(a);                             // 第三個:最早的(清單順序第一個 b)被取消
        Assert.Equal(new[] { a, c }, links.CircleItems);

        links.ToggleCircle(a);                             // 再按一次取消
        Assert.Equal(new[] { c }, links.CircleItems);
        links.ToggleCircle(c);
        Assert.Equal(new[] { a }, links.CircleItems);      // 都沒星號 → 回到第一個
    }

    [Fact]
    public void LinksCircle_DefaultsToDirectAction()
    {
        var links = NewLinks();
        var compact = (IIslandCompact)links;
        Assert.True(compact.CompactActionByDefault);
        Assert.False(string.IsNullOrEmpty(compact.CompactActionLabel));
        links.OnNumberHotkey(5);                           // 超出範圍:什麼都不做、不會丟例外
        links.OnNumberHotkey(0);
    }

    public void Dispose() => _host.Dispose();
}

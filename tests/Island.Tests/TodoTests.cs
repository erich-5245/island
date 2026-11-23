using Island.Modules.Todo;

namespace Island.Tests;

public class TodoTests
{
    [Fact]
    public void Add_CleansText_AndKeepsDoneAtBottom()
    {
        var l = new TodoList();
        var a = l.Add("  買牛奶 ")!;
        var b = l.Add("回信\n給老師")!;
        Assert.Equal("買牛奶", a.Text);
        Assert.Equal("回信 給老師", b.Text);
        Assert.Null(l.Add("   "));
        Assert.Null(l.Add(null));

        l.Toggle(a);                                   // a 完成 → 移到最下面
        Assert.Equal(new[] { b, a }, l.Items);
        var c = l.Add("繳電費")!;                      // 新的放在未完成的最後,完成的上面
        Assert.Equal(new[] { b, c, a }, l.Items);
        Assert.Equal(2, l.Pending);

        l.Toggle(a);                                   // 取消完成 → 回到未完成的最後
        Assert.Equal(new[] { b, c, a }, l.Items);
        Assert.False(a.Done);
        Assert.Equal(3, l.Pending);
    }

    [Fact]
    public void ClearDone_And_Remove()
    {
        var l = new TodoList();
        var a = l.Add("a")!; var b = l.Add("b")!; l.Add("c");
        l.Toggle(a); l.Toggle(b);
        Assert.Equal(2, l.ClearDone());
        Assert.Single(l.Items);
        Assert.True(l.Remove(l.Items[0]));
        Assert.Empty(l.Items);
    }

    [Fact]
    public void Limits()
    {
        var l = new TodoList();
        Assert.Equal(TodoList.MaxLength, l.Add(new string('x', 500))!.Text.Length);
        for (int i = 0; i < 200; i++) l.Add($"item {i}");
        Assert.Equal(TodoList.MaxItems, l.Items.Count);
    }

    [Fact]
    public void Json_RoundTrip_AndBadInput()
    {
        var l = new TodoList();
        var a = l.Add("寫報告")!;
        l.Add("運動");
        l.Toggle(a);
        var json = l.ToJson();
        Assert.Contains("寫報告", json);

        var back = new TodoList();
        back.LoadJson(json);
        Assert.Equal(new[] { "運動", "寫報告" }, back.Items.Select(i => i.Text));
        Assert.True(back.Items[1].Done);

        back.LoadJson("not json");
        Assert.Empty(back.Items);
        back.LoadJson("""[{"text":"  "}, {"text":"ok","done":true}, {"text":"first"}]""");
        Assert.Equal(new[] { "first", "ok" }, back.Items.Select(i => i.Text));   // 空白略過、未完成排前面
    }
}

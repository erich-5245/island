using System.Collections.ObjectModel;
using System.Text.Json;

namespace Island.Modules.Todo;

public sealed class TodoItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
}

/// <summary>待辦清單的純邏輯(不碰 UI,方便測試):未完成在上、完成的移到下面。</summary>
public sealed class TodoList
{
    public const int MaxLength = 200;
    public const int MaxItems = 100;

    public ObservableCollection<TodoItem> Items { get; } = [];

    public int Pending => Items.Count(i => !i.Done);

    /// <summary>新增一項(去頭尾空白、多行合成一行、太長截斷);空的或滿了回傳 null。</summary>
    public TodoItem? Add(string? text)
    {
        var t = Clean(text);
        if (t.Length == 0 || Items.Count >= MaxItems) return null;
        var item = new TodoItem { Text = t };
        // 放在未完成的最後面(完成的上面)
        int at = Items.TakeWhile(i => !i.Done).Count();
        Items.Insert(at, item);
        return item;
    }

    public void Toggle(TodoItem item)
    {
        int i = Items.IndexOf(item);
        if (i < 0) return;
        item.Done = !item.Done;
        Items.RemoveAt(i);
        if (item.Done) Items.Add(item);                                   // 完成 → 移到最下面
        else Items.Insert(Items.TakeWhile(x => !x.Done).Count(), item);   // 取消完成 → 回到未完成的最後
    }

    public bool Remove(TodoItem item) => Items.Remove(item);

    public int ClearDone()
    {
        var done = Items.Where(i => i.Done).ToList();
        foreach (var d in done) Items.Remove(d);
        return done.Count;
    }

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var one = string.Join(" ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return one.Length > MaxLength ? one[..MaxLength] : one;
    }

    // ── 存檔 ──

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson() => JsonSerializer.Serialize(Items.ToList(), JsonOptions);

    public void LoadJson(string json)
    {
        Items.Clear();
        List<TodoItem>? list;
        try { list = JsonSerializer.Deserialize<List<TodoItem>>(json, JsonOptions); }
        catch (JsonException) { return; }
        if (list is null) return;
        foreach (var i in list.Where(i => i is not null && Clean(i.Text).Length > 0).Take(MaxItems).OrderBy(i => i.Done))
            Items.Add(new TodoItem { Text = Clean(i.Text), Done = i.Done, Created = i.Created });
    }
}

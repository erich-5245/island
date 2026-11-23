using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Island.App.Infrastructure;

/// <summary>
/// 設定匯出 / 匯入:把 settings.json 和各模組的設定檔包成一個 .json,換電腦時一次搬過去。
/// 不包含:記錄檔、Shelf 清單與暫存圖片(路徑只在這台電腦有效)、行程的私人日曆網址(安全起見)。
/// </summary>
internal static class SettingsBundle
{
    public const string Format = "island-settings";
    public const int Version = 1;

    /// <summary>不匯出的檔案(相對於資料夾,用 / 分隔)。</summary>
    private static readonly string[] Excluded =
    [
        "modules/shelf/shelf.json",
        "modules/calendar/options.json",
    ];

    private static bool IsExcluded(string rel) =>
        Excluded.Contains(rel, StringComparer.OrdinalIgnoreCase) ||
        rel.StartsWith("modules/shelf/clips/", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Export(string dataDir)
    {
        var files = new JsonObject();
        var settings = Path.Combine(dataDir, "settings.json");
        if (File.Exists(settings)) files["settings.json"] = ParseOrNull(File.ReadAllText(settings));

        var modules = Path.Combine(dataDir, "modules");
        if (Directory.Exists(modules))
        {
            foreach (var file in Directory.EnumerateFiles(modules, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var rel = Path.GetRelativePath(dataDir, file).Replace('\\', '/');
                if (IsExcluded(rel)) continue;
                if (ParseOrNull(File.ReadAllText(file)) is { } node) files[rel] = node;
            }
        }
        var bundle = new JsonObject
        {
            ["format"] = Format,
            ["version"] = Version,
            ["exported"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            ["files"] = files,
        };
        return bundle.ToJsonString(Indented);
    }

    /// <summary>寫回設定檔;回傳寫了幾個檔案。格式不對時丟 InvalidDataException(不會寫任何東西)。</summary>
    public static int Import(string dataDir, string json)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { throw new InvalidDataException("不是 Island 的設定檔"); }
        if (root?["format"]?.GetValue<string>() != Format || root["files"] is not JsonObject files)
            throw new InvalidDataException("不是 Island 的設定檔");

        // 先全部檢查,沒問題才寫入
        var plan = new List<(string Path, string Content)>();
        var fullData = Path.GetFullPath(dataDir).TrimEnd('\\') + "\\";
        foreach (var (rel, node) in files)
        {
            if (node is null) continue;
            var clean = rel.Replace('\\', '/');
            bool allowed = clean == "settings.json" || (clean.StartsWith("modules/", StringComparison.Ordinal) && clean.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (!allowed || clean.Contains("..") || IsExcluded(clean)) continue;
            var target = Path.GetFullPath(Path.Combine(dataDir, clean));
            if (!target.StartsWith(fullData, StringComparison.OrdinalIgnoreCase)) continue;   // 不能寫到資料夾外面
            plan.Add((target, node.ToJsonString(Indented)));
        }
        foreach (var (target, content) in plan)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }
        return plan.Count;
    }

    private static JsonNode? ParseOrNull(string text)
    {
        try { return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { return null; }
    }
}

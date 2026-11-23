using Island.Core;

namespace Island.App.Infrastructure;

/// <summary>點小圓要「打開分頁」還是「直接執行」模組的動作。</summary>
internal static class CircleActions
{
    public const string Tab = "tab";
    public const string Action = "action";

    /// <summary>
    /// 使用者在設定 →「即時狀態」選過就照選的;沒選過照模組預設(捷徑 = 直接切換,其他 = 打開分頁)。
    /// 模組沒有直接動作時一律打開分頁。
    /// </summary>
    public static bool UseAction(IReadOnlyDictionary<string, string> choices, string moduleId, IIslandCompact module)
    {
        if (string.IsNullOrEmpty(module.CompactActionLabel)) return false;
        var choice = choices.FirstOrDefault(kv => string.Equals(kv.Key, moduleId, StringComparison.OrdinalIgnoreCase)).Value;
        return choice switch
        {
            Action => true,
            Tab => false,
            _ => module.CompactActionByDefault,
        };
    }
}

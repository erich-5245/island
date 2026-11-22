using Island.Core;

namespace Island.Modules.Shelf;

/// <summary>
/// Shelf(檔案暫存架)模組 — 第 1 階段只是空殼。
/// 第 2 階段:接收拖放的檔案、在展開畫面列出、可拖出;
/// 檔案數量改變時呼叫 SetBadge(new IslandBadge($"Shelf {count}")),外殼會自動更新膠囊右側。
/// </summary>
public sealed class ShelfModule : IslandModuleBase
{
    public override string Id => "shelf";
    public override string Title => "Shelf";

    // 第 1 階段:沒有收合畫面、沒有展開畫面、沒有 Badge(皆用基底類別的預設 null)。
}

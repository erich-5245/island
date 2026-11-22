namespace Island.Core;

/// <summary>收合膠囊右側的小提示。</summary>
/// <param name="Text">要顯示的文字,例如 "Shelf 3" 或 "3"。</param>
/// <param name="Glyph">選用的 Segoe Fluent Icons 字元(例如 ""),null 表示不顯示圖示。</param>
public sealed record IslandBadge(string Text, string? Glyph = null);

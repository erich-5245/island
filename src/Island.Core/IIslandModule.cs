using System.Windows;

namespace Island.Core;

/// <summary>
/// 動態島的一個功能模組(時間、Shelf、之後的媒體、通知…)。
/// 外殼在執行時掃描 Island.Modules.*.dll,找到實作此介面且有公開無參數建構子的類別就載入。
/// </summary>
/// <remarks>
/// 生命週期:建構 → <see cref="Initialize"/> → 視需要呼叫 Create*View → <see cref="IDisposable.Dispose"/>。
/// 所有成員都在 UI 執行緒上被呼叫;<see cref="BadgeChanged"/> 也必須在 UI 執行緒上觸發
/// (若從背景執行緒更新,請用 <see cref="IIslandHost.Dispatcher"/>)。
/// </remarks>
public interface IIslandModule : IDisposable
{
    /// <summary>穩定的識別字(小寫,例如 "clock"、"shelf"),用於 settings.json。</summary>
    string Id { get; }

    /// <summary>顯示用名稱。</summary>
    string Title { get; }

    /// <summary>外殼載入模組後呼叫一次。</summary>
    void Initialize(IIslandHost host);

    /// <summary>
    /// 收合狀態下膠囊中央的內容。回傳 null 表示此模組沒有收合畫面
    /// (只用 <see cref="Badge"/> 在右側顯示小提示)。
    /// 外殼使用清單中第一個回傳非 null 的模組作為中央內容。
    /// </summary>
    FrameworkElement? CreateCollapsedView();

    /// <summary>展開狀態下的內容(第 1 階段外殼尚未使用,第 3 階段起使用)。</summary>
    FrameworkElement? CreateExpandedView();

    /// <summary>收合時顯示在膠囊右側的小提示,例如「Shelf 3」。null 表示不顯示。</summary>
    IslandBadge? Badge { get; }

    /// <summary>模組通知外殼「<see cref="Badge"/> 變了」,外殼收到後重新讀取。</summary>
    event EventHandler? BadgeChanged;
}

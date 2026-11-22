namespace Island.Core;

/// <summary>選用介面:模組想知道自己的展開畫面何時被顯示/隱藏時實作(例如顯示時才重新整理)。</summary>
public interface IIslandExpandAware
{
    void OnExpanded();

    void OnCollapsed();
}

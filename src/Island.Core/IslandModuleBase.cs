using System.Windows;

namespace Island.Core;

/// <summary>方便實作模組的基底類別:處理 Host 保存與 Badge 變更通知。</summary>
public abstract class IslandModuleBase : IIslandModule
{
    private IslandBadge? _badge;

    public abstract string Id { get; }
    public abstract string Title { get; }

    /// <summary>預設是否啟用(見 <see cref="IIslandModule.EnabledByDefault"/>)。</summary>
    public virtual bool EnabledByDefault => true;

    protected IIslandHost Host { get; private set; } = null!;

    public IslandBadge? Badge => _badge;

    public event EventHandler? BadgeChanged;

    public void Initialize(IIslandHost host)
    {
        Host = host;
        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    public virtual FrameworkElement? CreateCollapsedView() => null;
    public virtual FrameworkElement? CreateExpandedView() => null;

    /// <summary>更新 Badge;值有變才通知外殼。可從任何執行緒呼叫。</summary>
    protected void SetBadge(IslandBadge? badge)
    {
        if (!Host.Dispatcher.CheckAccess())
        {
            Host.Dispatcher.BeginInvoke(() => SetBadge(badge));
            return;
        }
        if (Equals(_badge, badge)) return;
        _badge = badge;
        BadgeChanged?.Invoke(this, EventArgs.Empty);
    }

    public virtual void Dispose() => GC.SuppressFinalize(this);
}

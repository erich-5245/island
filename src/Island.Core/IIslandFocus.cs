namespace Island.Core;

/// <summary>
/// 選用介面:專注模式(例如番茄鐘專注中)。專注期間外殼:
/// 中間長條固定顯示這個模組、不放小圓、其他模組的跳出提示(換歌、下載完成…)暫停;
/// 音量亮度提示照常(使用者自己按的)。狀態改變時請觸發 <see cref="IIslandActivity.ActivityChanged"/>。
/// </summary>
public interface IIslandFocus
{
    bool IsFocusing { get; }
}

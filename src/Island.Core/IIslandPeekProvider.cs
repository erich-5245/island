using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:滑鼠移到膠囊上(一排字的「預覽」狀態)時,顯示在膠囊左側的內容,例如日期。
/// 外殼使用清單中第一個實作此介面且回傳非 null 的模組。
/// </summary>
public interface IIslandPeekProvider
{
    FrameworkElement? CreatePeekView();
}

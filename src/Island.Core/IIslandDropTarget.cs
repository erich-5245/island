using System.Windows;

namespace Island.Core;

/// <summary>
/// 選用介面:模組實作它就能接收拖到膠囊上的東西(檔案、文字…)。
/// 拖曳進入膠囊時,外殼依模組順序詢問 <see cref="CanAccept"/>,
/// 第一個回答 true 的模組會被展開,放開時呼叫 <see cref="Accept"/>。
/// </summary>
public interface IIslandDropTarget
{
    bool CanAccept(IDataObject data);

    void Accept(IDataObject data);
}

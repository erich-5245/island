# Island — Windows 動態島

獨立的 Windows 動態島程式(目標:取代 NUSA)。C# + WPF,.NET 10 LTS。
目前是 **第 1 階段**:外殼 + 時間膠囊 + 模組架構。

## 需求

- Windows 10/11
- .NET 10 SDK(`winget install Microsoft.DotNet.SDK.10`)。`global.json` 鎖定 10.0.404,允許更新的 feature band。

## 建置與執行

```powershell
dotnet build Island.sln -c Release
.\src\Island.App\bin\Release\net10.0-windows\Island.exe
```

發佈成單一資料夾(可複製到別處、加入開機啟動):

```powershell
dotnet publish src\Island.App -c Release -r win-x64 --self-contained false -o publish
```

- 以一般使用者權限執行(`asInvoker`)。**不要以系統管理員身分執行**:提權的視窗收不到一般程式拖過來的檔案(第 2 階段 Shelf 需要)。
- 同時只會有一個執行個體(Mutex `Local\Island.SingleInstance`),重複開啟會直接結束。
- 在膠囊上按右鍵 →「結束」。

## 設定

`%LOCALAPPDATA%\Island\settings.json`(第一次執行時自動建立):

```json
{
  "display": "primary",
  "topOffset": 8,
  "softwareRendering": true,
  "modules": ["clock", "shelf"],
  "disabledModules": []
}
```

| 欄位 | 說明 |
|---|---|
| `display` | `"primary"` = 跟著 Windows 主螢幕(預設)。或填裝置名稱,例如 `"\\\\.\\DISPLAY2"`,實際名稱請看 `island.log` 裡的「定位到 …」那一行。指定的螢幕不存在時自動退回主螢幕。 |
| `topOffset` | 膠囊離螢幕頂端的距離(DIP,100% 縮放時的像素)。 |
| `softwareRendering` | `true` = 軟體算圖,不建立 Direct3D 裝置,記憶體約省一半(見下方數據)。之後展開動畫若不順可改成 `false`。 |
| `modules` | 模組順序。第一個有收合畫面的模組放在膠囊中央;沒列出但有找到的模組會排在後面。 |
| `disabledModules` | 要停用的模組 Id。 |

修改設定後需要重新啟動 Island。log 檔在 `%LOCALAPPDATA%\Island\island.log`,只在事件發生時寫入。

## 專案結構

```
Island.sln
Directory.Build.props            共用設定(net10.0-windows、WPF、Nullable)
src/
  Island.App/                    外殼(exe 名稱 Island.exe)
    App.xaml(.cs)                啟動:Mutex、讀設定、載入模組、建立視窗
    MainWindow.xaml(.cs)         透明視窗 + 膠囊(左 | 中 | 右 三欄)、右鍵選單、定位
    app.manifest                 asInvoker + Per-Monitor V2 DPI
    Infrastructure/              AppPaths、Log、IslandSettings(settings.json)
    Interop/
      NativeMethods.cs           Win32 P/Invoke
      MonitorInfo.cs             列出螢幕、依設定選螢幕(實體像素 + DPI)
      DisplayWatcher.cs          螢幕插拔/DPI/喚醒 → 去抖動後重新定位
      TopmostKeeper.cs           前景視窗切換時重新設定 Topmost(事件驅動)
    Modules/
      ModuleLoader.cs            掃描 Island.Modules.*.dll 並建立模組
      IslandHost.cs              提供給模組的服務(Dispatcher、資料夾、log)
  Island.Core/                   模組合約(外殼和模組都引用它)
    IIslandModule.cs             Id、Title、Initialize、CreateCollapsedView、CreateExpandedView、Badge、BadgeChanged
    IslandBadge.cs               收合時右側的小提示(文字 + 選用圖示)
    IIslandHost.cs               外殼提供給模組的服務
    IslandModuleBase.cs          模組基底類別(SetBadge 會自動通知外殼)
  Modules/
    Directory.Build.props        所有模組自動引用 Island.Core
    Island.Modules.Clock/        時間模組:對齊整分鐘的單次計時器
    Island.Modules.Shelf/        空殼(第 2 階段實作)
```

### 模組如何被載入

1. `Island.App.csproj` 用萬用字元 `..\Modules\*\*.csproj` 引用所有模組,**目的只是讓它們一起建置、dll 複製到輸出目錄**。
2. 執行時 `ModuleLoader` 掃描 `Island.Modules.*.dll`,找出所有實作 `IIslandModule`、且有公開無參數建構子的類別。
3. 依照 `settings.json` 的 `modules` 排序,呼叫 `Initialize(host)`。

外殼程式碼裡完全沒有出現 `ClockModule` 或 `ShelfModule`。

### 新增模組

```powershell
dotnet new classlib -n Island.Modules.Xxx -o src\Modules\Island.Modules.Xxx -f net10.0
Remove-Item src\Modules\Island.Modules.Xxx\Class1.cs
```

```powershell
dotnet sln add --solution-folder Modules src\Modules\Island.Modules.Xxx\Island.Modules.Xxx.csproj
```

把 csproj 的 `PropertyGroup` 刪到只剩 `RootNamespace`(共用設定和 Island.Core 引用都來自 `Directory.Build.props`),然後繼承 `IslandModuleBase`:

```csharp
public sealed class XxxModule : IslandModuleBase
{
    public override string Id => "xxx";
    public override string Title => "Xxx";
    protected override void OnInitialize() { /* 訂閱事件… */ }
    // 有東西要顯示在膠囊右側時:
    // SetBadge(new IslandBadge("Xxx 3"));  // null = 不顯示
}
```

### 收合膠囊的版面

```
┌──────────────────────────────────┐
│ [左:預留]   [中:主模組]   [右:Badge…] │   200×36 DIP,#0A0A0A,圓角 18
└──────────────────────────────────┘
```

模組呼叫 `SetBadge(...)` 時會觸發 `BadgeChanged`,外殼收到後重新產生右側的 Badge 列。

## 視窗行為的實作方式

| 需求 | 做法 |
|---|---|
| 無邊框、透明 | `WindowStyle=None`、`AllowsTransparency=True`、`Background=Transparent` |
| 膠囊外點得穿 | AllowsTransparency 是逐像素 alpha 的 layered window,alpha=0 的像素系統會直接把滑鼠交給後面的視窗。不需要輪詢滑鼠位置。 |
| 不搶焦點 | `WS_EX_NOACTIVATE` + 攔截 `WM_MOUSEACTIVATE` 回傳 `MA_NOACTIVATE` + `ShowActivated=False` |
| 不出現在 Alt+Tab / 工作列 | `WS_EX_TOOLWINDOW`、移除 `WS_EX_APPWINDOW`、`ShowInTaskbar=False`。在第一次顯示前就套用。 |
| 維持最上層 | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`:前景視窗一換就 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`。事件驅動,閒置時不耗資源。 |
| 多螢幕與 DPI | Per-Monitor V2。位置以實體像素計算:`寬 = 640 DIP × 螢幕 DPI/96`,水平置中,`y = 螢幕頂端 + 8 × DPI/96`。 |
| 螢幕熱插拔 | `WM_DISPLAYCHANGE`、`WM_DPICHANGED`、`SystemEvents.DisplaySettingsChanged`、睡眠喚醒 → 400 ms 去抖動後定位,1.5 秒後再確認一次。拔掉外接螢幕時 Windows 會把筆電螢幕改成主螢幕,`"primary"` 模式自然會跟過去。 |

## 效能量測(2026-11-22,Release 建置,16 執行緒 CPU,1920×1080 @100%)

閒置(只有時間膠囊),啟動後等待穩定再量測:

| 指標 | 軟體算圖(預設) | GPU 算圖 |
|---|---|---|
| CPU(90 秒平均) | **47 ms / 90 s ≈ 0.05% 單核 ≈ 0.003% 整機** | – |
| 工作管理員「記憶體」(私用工作集) | **21.8 MB** | 47.2 MB |
| Private Bytes(已認可) | 45.9 MB | 73.1 MB |
| Working Set(含共用 DLL 頁面) | 93.6 MB | 132.0 MB |
| 執行緒 | 7 | 31 |

- dotnet-counters(`System.Runtime`,60 秒)顯示 **0 次 GC**;每 5 秒約 16–40 KB 的配置主要來自 dotnet-counters 本身的 EventPipe 工作階段。
- 時間每分鐘只更新一次(對齊到整分的單次計時器),沒有每幀或高頻輪詢。
- 工作管理員「詳細資料」預設的「記憶體(使用中的私人工作集)」就是 21.8 MB 那一欄。93.6 MB 的 Working Set 包含 WPF/.NET 共用 DLL 的頁面,這些頁面和其他 .NET 程式共用。

自行量測:

```powershell
dotnet tool install -g dotnet-counters
```

```powershell
dotnet-counters monitor -n Island --counters System.Runtime
```

## 驗收清單

| # | 項目 | 狀態 |
|---|---|---|
| 1 | 外接螢幕上方中間出現時間膠囊 | ✅ 已驗證(主螢幕 DISPLAY1,視窗 (640,8) 640×320,膠囊置中) |
| 2 | 膠囊旁的透明區域點得到後面的視窗 | ✅ `WindowFromPoint` 在透明區域回傳的是後面的程式 |
| 3 | 點膠囊不會讓正在打字的視窗失去焦點 | ✅ 模擬左鍵點擊膠囊,前後的前景視窗相同 |
| 4 | 拔掉/插回外接螢幕會換到正確螢幕 | ⏳ 需要你實際拔插測試(開發時只偵測到一個螢幕) |
| 5 | Alt+Tab 和工作列都看不到 | ✅ 視窗樣式:TOOLWINDOW=1、APPWINDOW=0(請再用 Alt+Tab 目視確認) |
| 6 | 右鍵「結束」可以關閉 | ✅ 模擬右鍵 → 點「結束」,程式已結束 |

**手動測試步驟**

1. 先結束 NUSA。兩個程式都在螢幕上方中間,而且都會搶 Topmost。
2. 執行 `Island.exe`。
3. 打開記事本開始打字,點一下膠囊,繼續打字:文字應該還會進到記事本。
4. 點膠囊左右兩側的空白處(仍在 640×320 的視窗範圍內):應該點到後面的程式。
5. 拔掉外接螢幕:約 0.5 秒內膠囊移到筆電螢幕上方中間。插回去:移回外接螢幕。兩個螢幕縮放比例不同時,膠囊大小看起來應該一樣。
6. 檢查 `%LOCALAPPDATA%\Island\island.log` 的「定位到 …」紀錄。
7. 按 Alt+Tab 並看工作列:都不應該出現 Island。
8. 右鍵膠囊 →「結束」。

## 已知事項

- 全螢幕程式(遊戲、影片)也會被膠囊蓋住。之後可以偵測全螢幕前景視窗並自動隱藏。
- 右鍵選單開啟時,選單本身可能短暫取得焦點(這是 WPF ContextMenu 的行為),關閉後就不會影響。
- 開機自動啟動尚未實作(可以先手動把捷徑放進 `shell:startup`)。

## 第 2 階段:Shelf(接下來要做)

1. **拖放接收**:膠囊/視窗加上 `AllowDrop`。拖曳物件進入膠囊時,外殼自動展開(在 640×320 預留的區域內)並顯示 Shelf 的放置區。
2. **展開/收合機制(外殼)**:
   - 外殼加上 `Collapsed` / `Expanded` 狀態,用 `CreateExpandedView()` 取得模組畫面。
   - 膠囊尺寸用動畫變化(只在動畫期間算圖,結束後回到閒置)。
   - 展開時視窗暫時可以接收滑鼠(拖放需要),收合後恢復點穿。
   - 多個模組時加上切換機制(例如展開畫面上方的分頁或圓點)。
3. **Shelf 模組**:
   - 保存檔案「參照」(路徑),選擇性地複製到 `%LOCALAPPDATA%\Island\modules\shelf\`。
   - 展開畫面列出檔案(圖示 + 名稱),可以拖出到其他程式(`DragDrop.DoDragDrop` + `DataFormats.FileDrop`)、移除單筆、全部清除。
   - 數量改變時 `SetBadge(new IslandBadge($"Shelf {n}"))`,膠囊右側自動顯示。
   - 重新啟動後保留清單(JSON)。
4. **拖放的權限**:Island 維持 `asInvoker`,確保一般權限的檔案總管可以拖進來(UIPI 會擋低權限 → 高權限)。
5. **效能維持**:閒置時不能有計時器或動畫;用 dotnet-counters 再量一次並更新上表。

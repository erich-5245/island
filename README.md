# Island — Windows 動態島

獨立的 Windows 動態島程式(目標:取代 NUSA)。C# + WPF,.NET 10 LTS。

- **第 1 階段(完成)**:外殼、時間膠囊、模組架構
- **第 2 階段(完成)**:hover 效果、展開/收合、模組分頁、拖放、Shelf

## 操作方式

| 動作 | 結果 |
|---|---|
| 平常(閒置) | **完全隱藏**。螢幕最上緣中間留一條看不見的熱區(寬同收合膠囊、高 = topOffset + 4 DIP)。右鍵「閒置時隱藏」取消勾選 → 改成平常顯示只有時間的小膠囊。 |
| 滑鼠推到螢幕上緣中間 / 移到膠囊上 | 出現「一排字」(320 寬)。**正在播放時**顯示封面 + 歌名 · 歌手;沒有播放時顯示左邊日期、中間時間、右邊 Shelf 數量。滑鼠離開約 0.2 秒後再隱藏。 |
| 正在播放音樂/影片時 | 平常一樣隱藏。**換歌時**膠囊出現約 3 秒(封面 + 歌名 · 歌手)再自動隱藏;暫停/繼續不會出現。右鍵「切歌時顯示」可關掉。 |
| 多個即時狀態(滑鼠移過去時) | 像 iPhone 動態島:**小圓 + 長條 + 小圓**。長條放 `liveOrder` 第一個有活動的(預設 音樂 > 計時),其餘「有東西可看」的模組最多兩個放小圓:兩個時左右各一,一個時放右邊(左邊保留看不見的空位,長條一直置中)。沒有活動時長條是日期/時間那一排,不放小圓。小圓內容:**計時**=剩餘時間進度環 + 分鐘數(不到 1 分鐘顯示秒數)、**音樂**=圓形封面、**天氣**=符號 + 溫度、**Shelf**=檔案圖示 + 數量。點小圓直接打開那個分頁。 |
| 左鍵點一下 | **固定展開**成 380×170,上方有模組分頁(時間/天氣/計時/播放/Shelf)。正在播放時直接打開「播放」分頁。滑鼠移開也不會收。 |
| 天氣 | 沒有音樂/計時時,滑鼠移過去那一排右側顯示「☀ 24°」(晚上是月亮)。**天氣分頁**:現在溫度、狀況、體感、今日高低溫與降雨機率、接下來 6 小時(降雨機率 ≥ 20% 時顯示)。**30 分鐘內可能下雨**(而現在沒下)時膠囊出現「☂ 30 分鐘內可能下雨 · 記得帶傘」約 5 秒,同一場雨只提醒一次。資料來源 Open-Meteo(免費、不需金鑰),每 15 分鐘更新。 |
| 計時分頁 | **計時器**:選 1/3/5/10/15/30/60 分(或在大時間上滾輪 ±1 分,執行中也能加減)→ 開始/暫停/重設。**番茄鐘**:專注 25 分 → 自動開始休息 5 分(每 4 輪休息 15 分)→ 休息結束提醒,按開始進入下一輪;可跳過這一段,4 個圓點顯示這組完成幾輪。 |
| 計時中 | 膠囊照常隱藏;滑鼠移到上緣顯示「碼錶 剩餘時間 狀態」(計時優先於音樂)。**時間到**時膠囊跳出「時間到」約 6 秒並播放提示音(Windows Notify Calendar),之後 30 秒內滑鼠移過去仍會看到,點一下打開計時分頁。 |
| 按音量鍵 / 調亮度 | 膠囊從上緣出現約 1.5 秒:**喇叭(或太陽)圖示 + 長條 + 數字**,連按會延長、長條即時跟著動;靜音顯示「靜音」。音量是任何方式改變系統音量都會顯示(鍵盤、工作列、其他程式);亮度只支援由 Windows 控制亮度的螢幕(筆電內建螢幕),外接螢幕用自己按鈕調的亮度 Windows 不知道。滑鼠停在島上時也會短暫換成提示。右鍵「音量亮度提示」可關掉。Windows 內建的提示框目前**無法隱藏**(這個 Windows 11 版本的提示框不是獨立視窗,沒有穩定的方法)。 |
| 播放分頁 | 封面、歌名、歌手、來源(Chrome、Spotify…)、進度條、上一首/播放暫停/下一首,以及**系統音量**滑桿(可拖曳、點擊跳到該位置、滾輪 ±2;左邊喇叭圖示切換靜音;用鍵盤音量鍵調整時滑桿會跟著動)。沒有播放時顯示「目前沒有正在播放的媒體」。 |
| 固定展開時點島以外的任何地方 | 收合(回到隱藏)。點島裡面(分頁、檔案)不會收。 |
| 把檔案拖到螢幕上緣中間或膠囊上 | 自動展開並切到 Shelf,邊框亮起;放開即加入。拖曳造成的展開不固定,滑鼠離開約 0.7 秒後收合。 |
| Shelf:拖曳某個檔案 | 拖到其他程式。行為和從檔案總管拖曳一樣:同一個磁碟預設是移動,不同磁碟是複製,可按 Ctrl/Shift 切換。檔案被移走後會自動從 Shelf 移除。 |
| Shelf:拖曳左上角「≡ N 個項目」 | 一次拖出全部 |
| Shelf:雙擊檔案 | 用預設程式開啟 |
| Shelf:右鍵檔案 | 開啟/在檔案總管中顯示/從 Shelf 移除 |
| Shelf:「全部清除」 | 清空清單(不會刪除檔案) |
| 右鍵膠囊 | 開機時啟動/全螢幕時隱藏/閒置時隱藏/切歌時顯示/音量亮度提示(都是開關)/結束 |
| 主螢幕上有全螢幕程式 | 膠囊自動隱藏(影片、遊戲、簡報、瀏覽器 F11、無邊框最大化的播放器);離開全螢幕或切到別的視窗就回來。只看膠囊所在的螢幕,另一個螢幕全螢幕不影響。 |

Shelf 只記住檔案**路徑**,不會複製檔案。清單存在 `%LOCALAPPDATA%\Island\modules\shelf\shelf.json`,重新啟動後保留;已經不存在的檔案會在展開時自動移除。收合時膠囊右側顯示「Shelf N」。

## 外觀

- 黑色膠囊;展開面板、分頁、進度條、音量條、膠囊邊框都使用**強調色**。
- **強調色跟著專輯封面**:播放中從封面取出代表色(縮成 24×24,依色相分 24 格,以飽和度加權投票),推出整組深淺(面板底色帶一點色調、分頁底色、亮色進度條…),換歌時 450 ms 漸變。黑白/灰階封面或沒在播放時用中性的黑白灰。
- **右鍵選單**:深灰底(#2C2C2E)+ 細邊框 + 圓角,可勾選的項目是開關,開啟時是強調色;點開關選單不會關閉。Shelf 檔案的右鍵選單也套用同樣樣式。

## 需求

- Windows 10/11
- .NET 10 SDK(`winget install Microsoft.DotNet.SDK.10`)。`global.json` 鎖定 10.0.404,允許更新的 feature band。

## 建置與執行

```powershell
dotnet build Island.sln -c Release
.\src\Island.App\bin\Release\net10.0-windows10.0.19041.0\Island.exe
```

**開機時啟動**:右鍵膠囊 → 勾選「開機時啟動」。會寫入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Island`,只影響目前使用者、不需要系統管理員權限,登入後以一般權限啟動。記錄的是「目前這個 Island.exe」的路徑;之後如果改從別的資料夾執行(例如 publish 資料夾),啟動時會自動把路徑更新成新的位置。

發佈成單一資料夾(可複製到別處):

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
  "collapsedWidth": 200,
  "collapsedHeight": 36,
  "peekWidth": 320,
  "expandedWidth": 380,
  "expandedHeight": 170,
  "hideWhenIdle": true,
  "hideInFullscreen": true,
  "softwareRendering": true,
  "announceActivity": true,
  "showOsd": true,
  "liveOrder": ["media", "timer", "weather", "shelf"],
  "liveHidden": [],
  "modules": ["clock", "weather", "timer", "media", "shelf", "osd"],
  "disabledModules": []
}
```

| 欄位 | 說明 |
|---|---|
| `display` | `"primary"` = 跟著 Windows 主螢幕(預設)。或填裝置名稱,例如 `"\\\\.\\DISPLAY2"`,實際名稱請看 `island.log` 裡的「定位到 …」那一行。指定的螢幕不存在時自動退回主螢幕。 |
| `topOffset` | 膠囊離螢幕頂端的距離(DIP,100% 縮放時的像素)。 |
| `collapsedWidth` / `collapsedHeight` | 平常的膠囊大小(只顯示時間) |
| `peekWidth` | 滑鼠移過去時變寬到多少(一排字,高度同收合) |
| `expandedWidth` / `expandedHeight` | 點擊展開後的大小(NUSA 展開約 400×175) |
| `hideWhenIdle` | `true` = 平常完全隱藏,滑鼠推到螢幕上緣中間才出現;`false` = 平常顯示小膠囊。右鍵選單「閒置時隱藏」會直接改這個值,立即生效。 |
| `liveOrder` | 即時狀態的優先順序:第一個有活動的放中間長條,後面的依序放小圓。例如想讓計時佔長條:`["timer", "media", "weather", "shelf"]`。 |
| `liveHidden` | 不要放進小圓的模組,例如 `["shelf"]`。 |
| `showOsd` | 調音量 / 亮度時顯示提示。右鍵「音量亮度提示」會直接改這個值。 |
| `announceActivity` | 換歌時讓膠囊出現約 3 秒。右鍵「切歌時顯示」會直接改這個值。 |
| `hideInFullscreen` | 膠囊所在螢幕有全螢幕程式時自動隱藏。右鍵選單的「全螢幕時隱藏」會直接改這個值,立即生效。 |
| `softwareRendering` | `true` = 軟體算圖,不建立 Direct3D 裝置,記憶體約省一半(見下方數據)。之後展開動畫若不順可改成 `false`。 |
| `modules` | 模組順序。第一個有收合畫面的模組放在膠囊中央;沒列出但有找到的模組會排在後面。 |
| `disabledModules` | 要停用的模組 Id。 |

**天氣位置**存在 `%LOCALAPPDATA%\Island\modules\weather\location.json`(預設新竹市東區 24.80, 120.99),改地名與經緯度後重新啟動 Island。只會把經緯度送給 Open-Meteo。

修改設定後需要重新啟動 Island。log 檔在 `%LOCALAPPDATA%\Island\island.log`,只在事件發生時寫入。

## 專案結構

```
Island.sln
Directory.Build.props            共用設定(net10.0-windows10.0.19041.0、WPF、Nullable)。指定 Windows 10 SDK 版本才能用 WinRT API(媒體控制)
src/
  Island.App/                    外殼(exe 名稱 Island.exe)
    App.xaml(.cs)                啟動:Mutex、讀設定、載入模組、建立視窗
    MainWindow.xaml(.cs)         透明視窗 + 膠囊(左 | 中 | 右 三欄)、右鍵選單、定位
    app.manifest                 asInvoker + Per-Monitor V2 DPI
    Infrastructure/              AppPaths、Log、IslandSettings(settings.json)、AutoStart、AccentTheme(強調色筆刷與漸變)
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
    IIslandHost.cs               外殼提供給模組的服務(Dispatcher、資料夾、log、Expand/Collapse/HoldExpanded)
    IIslandDropTarget.cs         選用:接收拖到膠囊上的東西(CanAccept / Accept)
    IIslandExpandAware.cs        選用:展開畫面顯示/隱藏時收到通知
    IIslandPeekProvider.cs       選用:滑鼠移過去(一排字)時左側的內容,Clock 用它顯示日期
    IIslandCompact.cs            選用:縮成小圓(IsCompactRelevant、CreateCompactView)
    IIslandActivity.cs           選用:「正在進行的活動」。有活動時預覽改顯示 CreateActivityView;IIslandHost.Announce 可讓膠囊短暫出現
    IslandModuleBase.cs          模組基底類別(SetBadge 會自動通知外殼)
    IslandTheme.cs               主題筆刷資源鍵(Island.Accent 等),模組用 DynamicResource 取用
  Modules/
    Directory.Build.props        所有模組自動引用 Island.Core
    Island.Modules.Clock/        時間模組:對齊整分鐘的單次計時器;展開畫面顯示大時間 + 日期
    Island.Modules.Timer/
      TimerModule.cs             計時器 / 番茄鐘狀態機(以結束時刻計算剩餘,睡眠喚醒不累積誤差)
      TimerView.xaml(.cs)        計時分頁:模式、大時間 + 進度、開始/重設/跳過、預設時間或輪數
      TimerActivityView.cs       計時中的一排(預覽、時間到提示)
    Island.Modules.Weather/
      WeatherModule.cs           抓取(HttpClient)、小提示、下雨提醒、睡眠喚醒後重抓
      WeatherData.cs             解析 Open-Meteo JSON、WMO 天氣代碼 → 文字 / 單色符號(Segoe UI Symbol)
      WeatherView.xaml(.cs)      天氣分頁
      WeatherAlertView.cs        下雨提醒的一排
    Island.Modules.Osd/
      OsdModule.cs               音量 / 亮度提示(沒有展開畫面,只用 Announce)
      OsdView.cs                 圖示 + 長條 + 數字
      AudioWatcher.cs            IAudioEndpointVolume 變更通知 + IMMNotificationClient(預設裝置切換時重新掛上)
      BrightnessWatcher.cs       WMI WmiMonitorBrightnessEvent(System.Management)
    Island.Modules.Media/
      MediaModule.cs             系統全域媒體控制(GlobalSystemMediaTransportControlsSessionManager):選工作階段、讀取資訊、控制
      MediaView.xaml(.cs)        播放分頁:封面、歌名、進度、控制按鈕
      MediaActivityView.cs       播放中的一排字(預覽、換歌提示)
      SystemVolume.cs            Core Audio IAudioEndpointVolume:系統主音量、靜音、變更通知
      AlbumColor.cs              從封面取代表色(灰階回傳 null)
    Island.Modules.Shelf/
      ShelfModule.cs             清單、拖入/拖出、保存、Badge
      ShelfView.xaml(.cs)        展開畫面:圖示格狀清單、標題列、空白提示
      ShelfItem.cs               一個項目(路徑、名稱、圖示)
      ShellIcons.cs              SHGetFileInfo 取得檔案總管圖示
```

### 模組如何被載入

1. `Island.App.csproj` 用萬用字元 `..\Modules\*\*.csproj` 引用所有模組,**目的只是讓它們一起建置、dll 複製到輸出目錄**。
2. 執行時 `ModuleLoader` 掃描 `Island.Modules.*.dll`,找出所有實作 `IIslandModule`、且有公開無參數建構子的類別。
3. 依照 `settings.json` 的 `modules` 排序,呼叫 `Initialize(host)`。

外殼程式碼裡完全沒有出現 `ClockModule` 或 `ShelfModule`。

### 新增模組

```powershell
dotnet new classlib -n Island.Modules.Xxx -o src\Modules\Island.Modules.Xxx -f net10.0-windows10.0.19041.0
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

### 展開與模組分頁

- 第一次展開時,外殼對每個模組呼叫 `CreateExpandedView()`,回傳非 null 的模組各得到一個分頁(分頁名稱 = `Title`)。只有一個模組時不顯示分頁。
- 點膠囊時展開到上次看的分頁。拖曳時展開到第一個 `CanAccept` 回傳 true 的模組。
- 模組可以呼叫 `Host.Expand("id")` / `Host.Collapse()`。執行長時間互動(例如拖出檔案)時,用 `using (Host.HoldExpanded()) { … }` 防止島自動收合。
- 自動收合的判斷:**只在展開期間**每 350 ms 用 `GetCursorPos` 確認游標是否還在島上;收合後計時器停止,閒置時沒有任何計時器在跑。

## 視窗行為的實作方式

| 需求 | 做法 |
|---|---|
| 無邊框、透明 | `WindowStyle=None`、`AllowsTransparency=True`、`Background=Transparent` |
| 膠囊外點得穿 | AllowsTransparency 是逐像素 alpha 的 layered window,alpha=0 的像素系統會直接把滑鼠交給後面的視窗。不需要輪詢滑鼠位置。 |
| 不搶焦點 | `WS_EX_NOACTIVATE` + 攔截 `WM_MOUSEACTIVATE` 回傳 `MA_NOACTIVATE` + `ShowActivated=False` |
| 不出現在 Alt+Tab / 工作列 | `WS_EX_TOOLWINDOW`、移除 `WS_EX_APPWINDOW`、`ShowInTaskbar=False`。在第一次顯示前就套用。 |
| 維持最上層 | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`:前景視窗一換就 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`。事件驅動,閒置時不耗資源。 |
| 多螢幕與 DPI | Per-Monitor V2。位置以實體像素計算:`寬 = 640 DIP × 螢幕 DPI/96`,水平置中,`y = 螢幕頂端 + 8 × DPI/96`。 |
| 全螢幕時隱藏 | 註冊成**不佔空間**的 AppBar(只 `ABM_NEW`,不 `ABM_SETPOS`)以收到系統的 `ABN_FULLSCREENAPP` 通知,再加上前景視窗切換事件,兩者都等 250 ms 讓視窗大小穩定後判斷一次,不輪詢。判斷:`SHQueryUserNotificationState` 回報 D3D 獨佔全螢幕/簡報模式;或依 Z 順序找出膠囊所在螢幕上最上面的一般視窗(略過工具視窗、被 DWM 隱藏的視窗、桌面與工作列),它蓋滿整個螢幕、且不是「有標題列的一般最大化」就算全螢幕。 |
| 閒置隱藏 + 熱區 | 視窗貼齊螢幕頂端,膠囊用 Margin 往下 `topOffset`。閒置時膠囊 Opacity = 0(像素 alpha 0,系統直接讓滑鼠穿透);熱區是 alpha 1/255 的細條,看不見但收得到滑鼠與拖放。熱區與膠囊包在同一個 Grid 裡,滑鼠進出只看這個 Grid。 |
| 點外面收合 | 不取得焦點的視窗收不到失焦事件,所以只在「固定展開」期間掛 `WH_MOUSE_LL`,收合時立刻卸下。掛勾裡只用 `WindowFromPoint` 判斷點到的視窗是不是本程式(膠囊、右鍵選單都算裡面),收合排到 UI 執行緒稍後執行。 |
| 正在播放 | `GlobalSystemMediaTransportControlsSessionManager`,和 Windows 音量提示框的媒體卡片是同一套,所以有接系統媒體控制的程式都支援。訂閱所有工作階段的播放狀態/歌曲資訊事件,優先顯示「正在播放」的那個;事件 60 ms 去抖動後才重新讀取。瀏覽器播放時約每 4～5 秒送一次時間軸更新,收合時直接忽略,展開時才讀;進度條只在展開於播放分頁且正在播放時每秒更新。封面以 160 px 解碼並凍結。換歌判斷:來源 + 標題 + 歌手改變且正在播放 → `IIslandHost.Announce(3 秒)`,啟動時讀到的第一首不算。音量只在展開時開啟 Core Audio 端點並註冊通知(每次重新取得預設裝置),收合即釋放;自己設定時帶固定的 event context GUID,收到通知時用它忽略自己造成的變更。 |
| 小圓 | 膠囊放進一個水平 StackPanel:[左小圓][膠囊][右小圓]。只在預覽、且長條顯示某個活動時計算;相關度變化跟著 ActivityChanged / BadgeChanged 重新排。有任何小圓時兩側都保留位置(空的一側 Hidden),長條保持置中。小圓的畫面只建立一次;計時進度環和其他每秒更新的畫面一樣只在看得到時更新。 |
| 天氣 | 每 15 分鐘一次 HTTP 請求(失敗 5 分鐘後重試;睡眠喚醒 10 秒後重抓;展開時資料超過 10 分鐘也重抓)。下雨判斷用 `minutely_15` 降水量:接下來兩格(30 分鐘)有 ≥ 0.1 mm 且現在沒下雨 → `Announce`。符號用 Segoe UI Symbol 的單色字元(WPF 不支援彩色 emoji)。 |
| 音量 / 亮度提示 | 全部事件驅動:Core Audio 音量通知、預設裝置切換通知、WMI 亮度事件。音量與靜音都沒變的通知(聲道平衡等)忽略。`Announce` 在滑鼠停在島上(預覽)時也有效:內容暫時換成提示,時間到換回預覽。 |
| 計時器 | 只在執行中有 1 秒的 DispatcherTimer。每秒只更新「看得到」的畫面:膠囊完全淡出後外殼把它設為 Hidden(WPF 不再算圖),模組用 `IsVisible` 判斷,畫面重新出現時(IsVisibleChanged)補更新。時間到用 `IIslandHost.Announce`。 |
| 強調色 | 主題筆刷放在 Application.Resources,所有用到的地方是 DynamicResource。放進資源字典的筆刷會被 WPF 凍結、不能做 ColorAnimation,所以換色時用計時器分 12 步、每步放一支新的凍結筆刷(只在換歌時跑)。模組透過 `IIslandHost.SetAccent(Color?)` 設定。 |
| 開機啟動 | `HKCU\...\Run`,值為 `"Island.exe 完整路徑"`。 |
| 螢幕熱插拔 | `WM_DISPLAYCHANGE`、`WM_DPICHANGED`、`SystemEvents.DisplaySettingsChanged`、睡眠喚醒 → 400 ms 去抖動後定位,1.5 秒後再確認一次。拔掉外接螢幕時 Windows 會把筆電螢幕改成主螢幕,`"primary"` 模式自然會跟過去。 |

## 效能量測(2026-11-22,Release 建置,16 執行緒 CPU,1920×1080 @100%)

### 正在播放(Chrome 播放 YouTube)

| 情況 | CPU | 工作管理員「記憶體」 | Private Bytes | Working Set | 執行緒 |
|---|---|---|---|---|---|
| 播放中、膠囊隱藏,沒有換歌的 60 秒(加入封面取色後) | **0 ms** | **35.8 MB** | — | — | — |
| 同上,但這 60 秒內換了一首歌(換歌提示動畫 + 取色 + 漸變) | 約 1.4 s | — | — | — | — |
| 播放中、膠囊隱藏,啟動 15 秒後量 60 秒(加入取色前) | 63 ms / 60 s ≈ 0.1% 單核 | 23.6 MB | — | — | — |
| (前一版:播放中常駐小膠囊)量 90 秒 | 109 ms / 90 s ≈ 0.12% 單核 | 24.3 MB | 49.5 MB | 114.6 MB | 10 |

第一版沒有忽略時間軸事件,同樣情況是 400 ms / 90 s,改了之後降到上面的數字。

### 第 2 階段(收合狀態閒置 60 秒)

| 情況 | CPU | 工作管理員「記憶體」 | Private Bytes | Working Set | 執行緒 |
|---|---|---|---|---|---|
| 剛啟動、還沒展開過 | 31 ms / 60 s ≈ 0.05% 單核 | **20.6 MB** | 44.7 MB | 89.2 MB | 9 |
| 展開過、拖入拖出過檔案後 | 63 ms / 60 s ≈ 0.1% 單核 | **44.6 MB** | 71.7 MB | 154.1 MB | 12 |

用過拖放後記憶體增加,主要是 Windows 殼層與 OLE 拖放元件(檔案圖示、拖曳影像、殼層擴充)載入到行程內,以及建立過的展開畫面。這些元件載入後會留在行程內,不會持續成長。工作管理員顯示的記憶體仍在 80 MB 目標以下。

### 第 1 階段

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

## 第 2 階段驗收(自動化測試,模擬真實滑鼠與 OLE 拖放)

| 項目 | 結果 |
|---|---|
| 點膠囊展開,前景視窗不變 | ✅ |
| 分頁顯示「時間 / Shelf」,時間頁顯示大時間 + 日期 | ✅ |
| 滑鼠離開後自動收合 | ✅ |
| 從另一個程式拖檔案到膠囊:自動展開到 Shelf、邊框亮起、放開後加入、寫入 shelf.json | ✅ |
| 從 Shelf 拖檔案到另一個程式:對方收到正確路徑,複製時原檔與清單都保留 | ✅ |
| 收合後顯示「Shelf 1」 | ✅ |
| 拖出時選「移動」,項目自動從清單移除 | ⏳ 尚未實測(請拖到同一顆磁碟的資料夾試試) |

## 第 1 階段驗收清單

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

## 全螢幕隱藏與開機啟動驗收(自動化測試)

| 情境 | 結果 |
|---|---|
| 主螢幕開一個無邊框、蓋滿螢幕的視窗 | ✅ 隱藏;關掉後出現 |
| 一般最大化視窗 | ✅ 不隱藏 |
| 另一個螢幕(筆電)全螢幕 | ✅ 不隱藏 |
| 同一個視窗從小視窗切成全螢幕再切回(模擬 F11,不換前景) | ✅ 隱藏 → 出現 |
| 無邊框最大化 | ✅ 隱藏 |
| 右鍵 → 勾選「開機時啟動」 | ✅ `HKCU\...\Run\Island` 寫入 exe 路徑 |

## 閒置隱藏 / 固定展開驗收(自動化測試,於 150% 的副螢幕執行,避開 NUSA)

| 步驟 | 結果 |
|---|---|
| 閒置 | ✅ 膠囊位置點得穿(底下是 Word);只有最上緣的熱區屬於 Island |
| 滑鼠推到上緣 | ✅ 出現一排字 |
| 滑鼠離開 | ✅ 再度隱藏、點得穿 |
| 點一下 → 滑鼠移開 1.8 秒 | ✅ 仍然展開,前景視窗沒被搶走 |
| 點島外面的視窗 | ✅ 收合並隱藏 |

## 正在播放驗收(實機:Chrome 播放 YouTube)

| 項目 | 結果 |
|---|---|
| 播放中、滑鼠沒碰 | ✅ 隱藏(膠囊位置點得穿) |
| 媒體鍵「下一首」換歌 | ✅ 膠囊出現(封面 + 新歌名),約 3 秒後自動隱藏 |
| 滑鼠推到上緣 | ✅ 播放中顯示封面 + 歌名 · 歌手 |
| 點一下 | ✅ 直接打開「播放」分頁,含音量列 |
| 音量滑桿滾輪 +2 / −2 | ✅ 外部讀取系統音量:10% → 12% → 10% |
| 暫停 / 播放按鈕 | ✅(前一版已測) |

### 計時器

| 情況 | CPU | 工作管理員「記憶體」 |
|---|---|---|
| 計時中、膠囊隱藏(只更新看得到的畫面) | **63 ms / 60 s** | 46.0 MB |
| 計時中、膠囊隱藏(第一版:隱藏時也每秒更新) | 281 ms / 60 s | 43.7 MB |

## 多個即時狀態驗收(實機)

| 情況 | 結果 |
|---|---|
| 音樂 + 天氣 | ✅ 長條「Lover · Taylor Swift」+ 右小圓「☾16°」,長條置中 |
| 音樂 + 計時 + 天氣 | ✅ 左小圓計時環「5」+ 長條音樂 + 右小圓天氣 |
| 點左小圓 | ✅ 打開計時分頁(4:45) |
| 閒置 60 秒 | 78 ms CPU、33.4 MB |

## 天氣驗收(實機)

| 項目 | 結果 |
|---|---|
| 抓取 | ✅ 新竹市東區:16°、晴、體感 16°、14–25°、降雨 10%,接下來 6 小時 |
| 一排字的小提示(暫停音樂時) | ✅「11/23 週一 · 01:44 · ☾ 16°」 |
| 下雨提醒 | 未實測(要等到真的快下雨) |
| 閒置 60 秒(連網抓完、執行緒池收回後) | 16 ms CPU、34.4 MB、18 執行緒(剛抓完那一分鐘約 190 ms、26 執行緒) |

## 音量 / 亮度提示驗收(實機)

| 項目 | 結果 |
|---|---|
| 監看啟動 | ✅ log:音量監看啟用、亮度監看啟用(筆電內建螢幕) |
| 按音量鍵 | ✅ 膠囊出現「喇叭 ━━━ 10」,約 2 秒後隱藏、位置點得穿 |
| 按靜音鍵 | ✅ 顯示靜音圖示與「靜音」,長條變暗 |
| 亮度 50 → 51 | ✅ 顯示「太陽 ━━━ 51」 |
| 閒置 60 秒 | 47 ms CPU、41.5 MB、12 執行緒 |

## 計時器驗收(實機)

| 項目 | 結果 |
|---|---|
| 選 1 分 → 開始 | ✅ 面板顯示 0:59、進度條、暫停鈕、重設鈕 |
| 收起後滑鼠移到上緣(Spotify 同時在播) | ✅ 顯示「碼錶 0:56 計時中」,計時優先於音樂;剩餘時間每秒更新 |
| 時間到 | ✅ 膠囊跳出「鈴鐺 時間到 · 點一下開啟」 |
| 點一下 | ✅ 打開計時分頁,顯示 0:00、時間到 |
| 番茄鐘畫面 | ✅ 25:00、重設/跳過/開始、4 個輪數圓點 |
| 番茄鐘完整一輪(25 分 → 自動休息 → 休息結束) | 未實測(需要 30 分鐘) |

## 已知事項

- 換歌提示(以及計時器時間到的提示)是一次膠囊動畫,軟體算圖下約花 1 秒多 CPU;只在事件發生時。

- 換歌時的提示動畫用軟體算圖,一次換歌約花 1 秒多 CPU(平均下來每首歌約 0.5% 單核);沒換歌時是 0。
- 有些網站換歌時會先送舊的封面再送新的,強調色可能短暫閃一下別的顏色。

- 熱區會擋住螢幕最上緣中間一小塊(預設 200 × 12 DIP)的點擊,例如剛好在那裡的瀏覽器分頁的最上緣幾個像素。
- 同時有多個來源在播放時,顯示系統列出的第一個正在播放的來源。
- 有些程式不提供封面或進度(例如部分網站),這時顯示預設圖示、隱藏進度條。
- 建置輸出資料夾改成 `net10.0-windows10.0.19041.0`。開機啟動的路徑會在新版 Island 啟動時自動更新。

- 全螢幕隱藏判斷的是「該螢幕上最上面的一般視窗」:全螢幕影片上方如果開了一個小視窗(例如在同一螢幕上跳出聊天視窗),膠囊會暫時出現,關掉小視窗就再隱藏。
- 右鍵選單開啟時,選單本身可能短暫取得焦點(這是 WPF ContextMenu 的行為),關閉後就不會影響。

## 之後可以做的(第 3 階段候選)

1. ~~全螢幕時自動隱藏~~、~~開機自動啟動~~:已完成。
3. ~~媒體模組~~:已完成。之後可加:點進度條跳轉(`TryChangePlaybackPositionAsync`)、多個來源切換。
6. ~~UI 設計~~:已完成(封面取色 + 深灰開關選單)。之後可再統一 Shelf / 時間分頁的細節。
4. **Shelf 進階**:拖入文字/網址/圖片(目前只接收檔案)、多選、較大的縮圖(`IShellItemImageFactory`)、選擇性複製到 Shelf 資料夾。
5. **設定畫面**:在右鍵選單加「設定」,選螢幕、開關模組,不用手動改 JSON。

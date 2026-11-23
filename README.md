# Island — Windows 動態島

獨立的 Windows 動態島程式(目標:取代 NUSA)。C# + WPF,.NET 10 LTS。

- **第 1 階段(完成)**:外殼、時間膠囊、模組架構
- **第 2 階段(完成)**:hover 效果、展開/收合、模組分頁、拖放、Shelf

## 操作方式

| 動作 | 結果 |
|---|---|
| 平常(閒置) | **完全隱藏**。螢幕最上緣中間留一條看不見的熱區(寬同收合膠囊、高 3 DIP,只佔螢幕最頂端,幾乎不會擋到瀏覽器分頁)。右鍵「閒置時隱藏」取消勾選 → 改成平常顯示只有時間的小膠囊。 |
| 滑鼠推到螢幕上緣中間 / 移到膠囊上 | 出現「一排字」(320 寬)。**正在播放時**顯示封面 + 歌名 · 歌手;沒有播放時顯示左邊日期、中間時間、右邊 Shelf 數量。滑鼠離開約 0.2 秒後再隱藏。 |
| 正在播放音樂/影片時 | 平常一樣隱藏。**換歌時**膠囊出現約 3 秒(封面 + 歌名 · 歌手)再自動隱藏;暫停/繼續不會出現。右鍵「切歌時顯示」可關掉。 |
| 多個即時狀態(滑鼠移過去時) | 像 iPhone 動態島:**小圓 + 長條 + 小圓**。長條放 `liveOrder` 第一個有活動的(預設 音樂 > 計時),其餘「有東西可看」的模組最多兩個放小圓:兩個時左右各一,一個時放右邊(左邊保留看不見的空位,長條一直置中)。沒有活動時長條是日期/時間那一排,只放「沒有活動也顯示」的小圓(目前是**捷徑**)。除了捷徑小圓,其他小圓點一下會打開那個分頁。小圓內容:**計時**=剩餘時間進度環 + 分鐘數(不到 1 分鐘顯示秒數)、**音樂**=圓形封面、**天氣**=符號 + 溫度、**Shelf**=檔案圖示 + 數量、**捷徑**=**一個**捷徑的圖示(設定 →「捷徑」標星號的那個,沒標就用第一個;有捷徑時一直都在,**點一下直接切到 / 打開那個視窗**,其他捷徑展開島在捷徑分頁點;預設排在 liveOrder 最後,可在設定「即時狀態」調整或關掉)。點小圓直接打開那個分頁。 |
| **快捷鍵 Ctrl+Alt+I** | 叫出並固定展開(不搶焦點);展開時按 **Esc** 或再按一次收起。Esc 只在用快捷鍵打開時才攔截,收起後立刻放掉。可在設定 →「一般」→「快捷鍵」點輸入框後直接按下想要的組合;被其他程式佔用時會提示。(原本預設 Ctrl+Alt+Space,但在這台電腦已被其他程式佔用。) |
| 左鍵點一下 | **固定展開**成 380×170,上方有模組分頁(時間/天氣/計時/播放/Shelf/捷徑),分頁那一排最右邊的**齒輪**直接打開設定(島同時收起)。正在播放時直接打開「播放」分頁。滑鼠移開也不會收。 |
| 天氣 | 沒有音樂/計時時,滑鼠移過去那一排右側顯示「☀ 24°」(晚上是月亮)。**天氣分頁**:現在溫度、狀況、體感、今日高低溫與降雨機率、接下來 6 小時(降雨機率 ≥ 20% 時顯示)。**30 分鐘內可能下雨**(而現在沒下)時膠囊出現「☂ 30 分鐘內可能下雨 · 記得帶傘」約 5 秒,同一場雨只提醒一次。資料來源 Open-Meteo(免費、不需金鑰),每 15 分鐘更新。 |
| 計時分頁 | **計時器**:選 1/3/5/10/15/30/60 分(或在大時間上滾輪 ±1 分,執行中也能加減)→ 開始/暫停/重設。**番茄鐘**:專注 25 分 → 自動開始休息 5 分(每 4 輪休息 15 分)→ 休息結束提醒,按開始進入下一輪;可跳過這一段,4 個圓點顯示這組完成幾輪。 |
| 計時中 | 膠囊照常隱藏;滑鼠移到上緣顯示「碼錶 剩餘時間 狀態」(計時優先於音樂)。**時間到**時膠囊跳出「時間到」約 6 秒並播放提示音(Windows Notify Calendar),之後 30 秒內滑鼠移過去仍會看到,點一下打開計時分頁。 |
| 捷徑分頁 | 展開面板的「捷徑」分頁放常用的程式 / 資料夾 / 網址 / 檔案。**點一下:已經開著 → 把那個視窗切到最前面**(最小化的先還原,開了好幾個視窗就切到最近用過的那個);**沒開 → 打開**。Microsoft Store 程式(例如 Claude)用應用程式識別碼啟動。右鍵捷徑可往前 / 往後移、移除。加入方式:設定 →「捷徑」從**開著的視窗**直接加入、選擇程式或資料夾、輸入網址;或在「捷徑」分頁開著時把程式、捷徑(.lnk)、資料夾、網址拖到島上(拖曳經過分頁標籤時會切到那個分頁)。清單存在 `modules\links\links.json`。 |
| 麥克風 / 鏡頭使用中 | 像 iPhone 的橘點 / 綠點:有程式開始用麥克風或鏡頭時,膠囊出現約 3 秒「● 麥克風使用中 · 程式名稱」;使用期間滑鼠移過去是一個活動或小圓(橘點 / 綠點)。讀的是 Windows 記錄「誰在用麥克風 / 鏡頭」的位置(工作列的麥克風圖示也是讀這裡),用登錄變更通知,不輪詢。 |
| 下載 | 瀏覽器下載中(Chrome / Edge `.crdownload`、Firefox `.part`):滑鼠移過去顯示「⬇ 下載中 · 檔名 · 已下載大小」或小圓「⬇ 12M」(瀏覽器不告訴檔案系統總大小,所以沒有百分比)。完成:膠囊出現「✓ 下載完成 · 檔名 · 大小」約 4 秒,檔案**自動放進 Shelf**(設定 →「下載」可關掉)。 |
| 插拔電源 / 電量低 | 插上:「⚡ 充電中 78%」(綠色);拔掉:「使用電池 78%」;充到 100%:「已充飽」;沒插電降到 20% 與 10% 各提醒一次(紅色)。充電中或電量偏低時,滑鼠移過去會佔一個小圓:電量環 + 百分比。只在有電池的電腦上啟用。 |
| 截圖 | Win+Shift+S / Print Screen 截圖後(Windows 會自動存到「螢幕擷取畫面」資料夾),截圖**自動放進 Shelf**,膠囊出現「縮圖 + 截圖已放進 Shelf」約 3 秒,點開 Shelf 就能拖到聊天或文件裡。`modules\shelf\options.json` 的 `captureScreenshots` 設為 false 可關閉。 |
| 按音量鍵 / 調亮度 | 膠囊從上緣出現約 1.5 秒:**喇叭(或太陽)圖示 + 長條 + 數字**,連按會延長、長條即時跟著動;靜音顯示「靜音」。音量是任何方式改變系統音量都會顯示(鍵盤、工作列、其他程式);亮度只支援由 Windows 控制亮度的螢幕(筆電內建螢幕),外接螢幕用自己按鈕調的亮度 Windows 不知道。滑鼠停在島上時也會短暫換成提示。右鍵「音量亮度提示」可關掉。Windows 內建的提示框目前**無法隱藏**(這個 Windows 11 版本的提示框不是獨立視窗,沒有穩定的方法)。 |
| 播放分頁 | 封面、歌名、歌手、來源(Chrome、Spotify…)、進度條(**點一下跳到那個位置**,來源不支援跳轉時不能點)、上一首/播放暫停/下一首,以及**系統音量**滑桿(可拖曳、點擊跳到該位置、滾輪 ±2;左邊喇叭圖示切換靜音;用鍵盤音量鍵調整時滑桿會跟著動)。沒有播放時顯示「目前沒有正在播放的媒體」。 |
| 固定展開時點島以外的任何地方 | 收合(回到隱藏)。點島裡面(分頁、檔案)不會收。 |
| 把檔案、文字、網址或圖片拖到螢幕上緣中間或膠囊上 | 自動展開並切到 Shelf,邊框亮起;放開即加入。拖曳造成的展開不固定,滑鼠離開約 0.7 秒後收合。 |
| Shelf:拖曳某個檔案 | 拖到其他程式。行為和從檔案總管拖曳一樣:同一個磁碟預設是移動,不同磁碟是複製,可按 Ctrl/Shift 切換。檔案被移走後會自動從 Shelf 移除。 |
| Shelf:拖曳左上角「≡ N 個項目」 | 一次拖出全部 |
| Shelf:雙擊 | 檔案、圖片用預設程式開啟;網址用瀏覽器開啟;文字複製到剪貼簿 |
| Shelf:右鍵項目 | 開啟/複製/在檔案總管中顯示(檔案、圖片才有)/從 Shelf 移除 |
| Shelf:「貼上」 | 把剪貼簿的檔案、文字、網址或圖片加進 Shelf(空的時候中間也有「貼上剪貼簿」) |
| Shelf:「全部清除」 | 清空清單(不會刪除你的檔案;Shelf 自己存的圖片會一起刪掉) |
| 右鍵膠囊 | 開機時啟動/全螢幕時隱藏/閒置時隱藏/切歌時顯示/音量亮度提示(都是開關)/**設定…**/結束 |
| 主螢幕上有全螢幕程式 | 膠囊自動隱藏(影片、遊戲、簡報、瀏覽器 F11、無邊框最大化的播放器);離開全螢幕或切到別的視窗就回來。只看膠囊所在的螢幕,另一個螢幕全螢幕不影響。 |

Shelf 收四種項目:

| 種類 | 怎麼來 | 存在哪 | 拖出去 |
|---|---|---|---|
| 檔案 / 資料夾 | 從檔案總管、桌面、下載完成、截圖 | 只記**路徑**,不複製檔案 | 和檔案總管一樣(可移動) |
| 文字 | 從瀏覽器、Word、記事本選取文字拖過來,或複製後按「貼上」 | 存在 shelf.json | 文字(只複製) |
| 網址 | 拖網址列 / 連結,或單行的 `http(s)://`、`www.`、`mailto:` 文字 | 存在 shelf.json,圖塊顯示網域 | 文字(只複製) |
| 圖片 | 從瀏覽器拖圖片(虛擬檔案)、複製圖片後按「貼上」 | 存成 `modules\shelf\clips\` 裡的檔案,移除時刪掉 | 檔案(只複製,拖出後仍留在 Shelf);右鍵「複製」會同時放圖片本身,可直接貼到聊天軟體 |

同一次拖曳有多種格式時,依序選 檔案 → 虛擬檔案 → 圖片 → 文字,只加入一份(例如從瀏覽器拖圖片會存圖,不會多一個網址)。重複的項目不會加入。

清單存在 `%LOCALAPPDATA%\Island\modules\shelf\shelf.json`,重新啟動後保留;已經不存在的檔案會在展開時自動移除。收合時膠囊右側顯示「Shelf N」。

## 圖示

<img src="src/Island.App/Assets/Island-512.png" width="96" align="right" />

俯瞰的環礁:深藍色的海上,一圈金色沙灘圍成**膠囊形狀**的潟湖(也就是 Island 本身的形狀),沙灘上有椰子樹、潟湖裡有一艘小船。32 px 以下另外畫簡化版(只留海、金色膠囊和一棵椰子樹),工作列上才不會糊掉。

- 圖示檔:`src/Island.App/Assets/Island.ico`(16、20、24、32、40、48、64、96、128、256 px,PNG 壓縮),同時是 exe 圖示和設定視窗的圖示。
- 原始碼:`tools/app-icon/AtollIcons.cs`(用 System.Drawing 畫,另外保留黑金、霧青兩組配色);重新產生:`powershell -ExecutionPolicy Bypass -File tools\app-icon\make-icon.ps1`。

## 外觀

- **出現 / 隱藏動畫**:隱藏時整列(小圓 + 膠囊,或展開的面板)維持原樣**往上滑出螢幕**(230 ms,加速離開),看不見之後才重設成收合狀態;出現時直接以目標大小**從螢幕上方滑下來**(320 ms,帶一點回彈)。滑到一半又把滑鼠移回來,會直接滑回來。島本來就看得到時(例如預覽變展開)仍然是放大/縮小的動畫。

- 黑色膠囊;展開面板、分頁、進度條、音量條、膠囊邊框都使用**強調色**。
- **強調色跟著專輯封面**:播放中從封面取出代表色(縮成 24×24,依色相分 24 格,以飽和度加權投票),推出整組深淺(面板底色帶一點色調、分頁底色、亮色進度條…),換歌時 450 ms 漸變。黑白/灰階封面或沒在播放時用中性的黑白灰。
### 新功能模組

| 模組 | 預設 | 說明 |
|---|---|---|
| 待辦 | 開 | 展開「待辦」分頁:點一項勾選(完成的移到下面、劃線)、× 刪除、清除已完成;「＋ 新增」跳出小輸入框(Enter 新增、Esc 取消)。還有沒做完的事時,滑鼠移過去那一排右邊顯示「待辦 N」。存在 `modules	odo	odos.json`。 |
| 行程 | 關 | 貼上日曆的 iCal 私人網址(Google / Outlook / Apple 都有)。行程前 5/10/15/30 分鐘島跳出「10 分鐘後 · 週會」,一直顯示到開始;「行程」分頁看今天和明天;那一排右邊顯示「下個行程 14:00」。支援時區、全天、重複規則(每天 / 每週幾 / 每月 / 每年、間隔、次數、截止日)、例外日期、單次修改。每 15 分鐘更新一次,網址不寫進記錄檔。 |
| 剪貼簿 | 關 | 記住最近複製的 20 筆文字,點一下重新複製、或丟進 Shelf。**只存在記憶體**(關掉 Island 就消失);密碼管理器標記「不要記錄」的內容會略過。用 Windows 通知,不輪詢。 |
| 系統 | 關 | CPU 進度環小圓(沒有其他活動時也顯示)+「系統」分頁:CPU、記憶體長條、網路上下行速度。只在看得到時每秒取樣一次;小圓可設定成點了打開工作管理員。 |
| 專注模式(計時) | 開 | 番茄鐘專注的 25 分鐘內,島只顯示倒數:換歌、下載完成等提示暫停,也不放小圓;休息時恢復。音量亮度提示照常。設定 →「計時」可關。 |
| 音樂律動(播放) | 開 | 播放時長條右邊 4 根小柱子跟著音量跳動(讀系統輸出的音量峰值,不錄音)。只在看得到、正在播放時每秒更新 20 次;設定 →「播放」可關。 |

預設關閉的模組在設定 →「模組」打開(標示「預設關閉」),重新啟動後出現。

### 第一次使用與搬家

- **歡迎頁**:第一次啟動(還沒有 settings.json)時自動打開設定的「歡迎」頁:① 試試看叫出島 ② 選天氣城市 ③ 挑主題。之後也可以在設定左邊的「歡迎」再看一次。
- **匯出 / 匯入設定**:設定 →「一般」→「備份與搬家」。所有設定、主題、捷徑、待辦、各模組選項存成一個 `.json`;匯入後可以馬上重新啟動套用。不包含 Shelf 清單(路徑只在原本的電腦有效)和私人日曆網址;匯入時只會寫入 Island 自己的資料夾。

### 小圓與快捷鍵

- **點小圓的動作**:設定 →「即時狀態」每一列可以選「點了打開分頁」或直接執行:播放 / 暫停(音樂)、暫停 / 繼續(計時)、打開天氣網站(天氣)、貼上剪貼簿(Shelf)、打開下載資料夾(下載)、打開電源設定(電量)、打開隱私權設定(麥克風 / 鏡頭)。捷徑小圓預設是直接切到那個視窗,其他預設打開分頁。
- **兩個捷徑小圓**:設定 →「捷徑」最多標兩個星號,就有兩顆捷徑小圓(小圓總共最多兩顆,和其他狀態一起排)。標第三個時,清單裡較前面的那個星號會被取消。
- **小圓延伸出開著的視窗**:滑鼠停在第一個捷徑小圓上約 0.2 秒,小圓往外(右邊的往右、左邊的往左)拉長成一條:`[捷徑] | 開著的程式…`。同一個程式的視窗合成一格(右上角小數字),Microsoft Store 程式每個視窗各一格;所有螢幕的視窗都列出來,依最近使用排序,最多 8 格,其餘收進「+N」選單。白色長條 = 現在在前面的程式。點一格切過去(已經在前面就輪到它的下一個視窗),中鍵關閉。只在延伸時用 WinEvent 監看視窗開 / 關 / 換前景,平常不耗資源。有可延伸的小圓時,一排的視窗兩側先預留延伸空間(延伸當下不改視窗大小,島才不會抽動);小圓寬度取整數像素動畫,中間長條不會晃。設定 →「捷徑」可關掉。
- **開始按鈕**:點一下打開 Windows 開始功能表(送出一次 Windows 鍵)。設定 →「一般」→「開始」按鈕 選位置:一排的左邊(預設,日期前面,跟著日期淡入)、小圓延伸列(開著的視窗後面)、展開分頁列(齒輪旁)、不顯示。
- **自動隱藏 Windows 工具列**:設定 →「一般」的開關,和 Windows 設定裡「自動隱藏工作列」是同一個設定(SHAppBarMessage),顯示的是目前實際狀態;關掉 Island 不會改回來。
- **展開後的分頁列**:分頁和右側圖示(開始、設定)都是 24 高、垂直置中;放得下時分頁在整個面板置中,放不下時可用滾輪左右捲,被切到的那側會淡出,切換分頁時自動捲到看得到。
- **Ctrl+Alt+1…9**:直接切到第 1…9 個捷徑,不用打開島。設定 →「一般」可以關掉;被其他程式佔用的組合會記在 island.log。

### 主題(設定 →「主題」,全部立即套用)

| 項目 | 選項 |
|---|---|
| 預設主題 | 經典黑、深藍金、霓虹、森林、日夜、石墨、玻璃、極簡;點小卡一鍵套用,島會出現 3 秒預覽 |
| 強調色來源 | 跟著專輯封面(預設)/ 跟著 Windows 主題色(改了會自動跟著)/ 固定顏色(12 色色盤、色碼、「自訂…」開 Windows 色彩對話方塊)/ 依時段變化(清晨橘 → 白天藍 → 傍晚紫 → 深夜靛,每 15 分鐘漸變)/ 不使用 |
| 膠囊 | 底色(10 個深色色盤 + 色碼 + 自訂)、不透明度 60–100%(半透明玻璃感)、圓角 0–100%(膠囊 ↔ 圓角方形,小圓也跟著)、細邊框開關 |
| 文字與動畫 | 字型(系統字型清單,圖示不受影響)、動畫速度 快 / 標準 / 慢(0.6× / 1× / 1.6×) |
| 我的主題 | 取名後「存成我的主題」,出現在主題清單,右鍵可刪除 |
| 分享 | 匯出成 `.json`、匯入別人的主題(檔案內容會檢查並修正到合理範圍,不是主題檔會提示) |

**「時間」分頁背景圖**:設定 →「時間」→「選擇圖片…」(jpg / png / bmp / webp;動態圖只顯示第一格)。圖片複製一份到 `modules\clock\`,讀進來時縮到面板大小(約 1–2 MB 記憶體);可調暗度 0–80%、可選模糊(選圖時算一次)。圖上的時間和日期加淡淡陰影。不包含在匯出的設定或主題裡。

**時鐘樣式**(像 iPhone 鎖定畫面):設定 →「時間」最上面有即時預覽。字體五選一(圓潤、窄體、粗黑、襯線、等寬)+ 粗細滑桿;顏色:從背景圖取色(取不透明、偏鮮豔的像素平均,再調到在暗色遮罩上看得清楚的亮度)、白 / 黃 / 橘 / 粉 / 紫 / 藍 / 綠、自訂(Windows 色彩對話方塊),加深淺滑桿(雙擊回原色);實線或玻璃(上亮下透的漸層)。可選「日期也用這個顏色」「小膠囊的時間也套用」(預設關)。只在改設定時重算,筆刷都凍結,不增加閒置成本;只有選了「從背景圖取色」才會讀圖。

設定視窗所有下拉選單改成深灰圓角樣式(清單裡目前選的項目用強調色加勾號)。

主題存在 `settings.json` 的 `theme` / `customThemes`。時間的 12 / 24 小時制在設定 →「時間」。效能:只有「依時段變化」每 15 分鐘醒來一次,「跟著 Windows」只在 Windows 通知色彩改變時才讀登錄,其他模式不多花任何 CPU。

- **右鍵選單**:深灰底(#2C2C2E)+ 細邊框 + 圓角,可勾選的項目是開關,開啟時是強調色;點開關選單不會關閉。Shelf 檔案的右鍵選單也套用同樣樣式。

## 需求

- Windows 10/11
- .NET 10 SDK(`winget install Microsoft.DotNet.SDK.10`)。`global.json` 鎖定 10.0.404,允許更新的 feature band。

## 建置與執行

```powershell
dotnet build Island.sln -c Release
.\src\Island.App\bin\Release\net10.0-windows10.0.19041.0\Island.exe
```

### 自動測試

```powershell
dotnet test tests\Island.Tests
```

只測純邏輯,不開視窗、不動滑鼠、不碰系統設定,約 1 秒跑完(163 個測試):

| 檔案 | 測什麼 |
|---|---|
| `ShelfTests.cs` | 文字 / 網址判斷、標題截斷、FILEGROUPDESCRIPTORW 檔名解析、安全檔名、不重複檔名、shelf.json 新舊格式 |
| `ShelfModuleTests.cs` | 用假的外殼(`FakeHost`)和假的拖曳資料跑 Shelf:收文字、網址、圖片、瀏覽器虛擬檔案,格式優先順序,移除 / 清除時只刪 Shelf 自己存的檔案,重新啟動後還在 |
| `ThemeTests.cs` | 主題:色碼解析、修正不合理值、預設主題都夠暗且互不相同、圓角與動畫倍率、依時段的顏色漸變、匯出匯入與壞檔案 |
| `LinksTests.cs` | 捷徑小圓代表哪一個(沒標星號用第一個、最多兩個星號、重新啟動後保留、移除後回到第一個)、數字快捷鍵超出範圍 |
| `CalendarTests.cs` | iCal 解析:UTC / 時區 / 浮動時間、全天、折行與跳脫字元、取消、VALARM 不混入、每週幾 + 次數 + 例外日期、每兩天 + 截止日、每月 / 每年、單次修改;提醒時刻與文字 |
| `TodoTests.cs` / `ClipboardTests.cs` | 待辦排序(完成的移到下面)、長度與數量上限、存檔;剪貼簿紀錄去重、上限、預覽文字 |
| `SysMonTests.cs` | CPU% 計算、網速與格式、實際取樣;預設關閉的模組開關(透過介面讀也正確) |
| `SettingsBundleTests.cs` | 匯出匯入來回、不含私人網址與 Shelf 路徑、不是設定檔時不寫入、不能寫到資料夾外面 |
| `CircleActionTests.cs` | 點小圓「打開分頁 / 直接執行」:使用者選擇優先、沒選照模組預設、沒有動作的模組一律打開分頁 |
| `LogicTests.cs` | 番茄鐘完整 8 輪(第 4、8 輪後長休息)、時間格式、下雨提醒(只提醒一次、轉晴重設、正在下不提醒)、Open-Meteo JSON 解析、封面取色、快捷鍵解析 / 格式化、設定尺寸修正 |

測試要用到的 `internal` 類別由 `Directory.Build.props` 的 `InternalsVisibleTo` 開放給 `Island.Tests`。下雨提醒和番茄鐘輪替抽成純函式(`RainAlert.ShouldAlert`、`TimerModule.NextPhase`)以便測試,模組行為不變。

**開機時啟動**:右鍵膠囊 → 勾選「開機時啟動」。會寫入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Island`,只影響目前使用者、不需要系統管理員權限,登入後以一般權限啟動。記錄的是「目前這個 Island.exe」的路徑;之後如果改從別的資料夾執行(例如 publish 資料夾),啟動時會自動把路徑更新成新的位置。

發佈成單一資料夾(可複製到別處):

```powershell
dotnet publish src\Island.App -c Release -r win-x64 --self-contained false -o publish
```

- 以一般使用者權限執行(`asInvoker`)。**不要以系統管理員身分執行**:提權的視窗收不到一般程式拖過來的檔案(第 2 階段 Shelf 需要)。
- 同時只會有一個執行個體(Mutex `Local\Island.SingleInstance`),重複開啟會直接結束。
- 在膠囊上按右鍵 →「結束」。

## 設定

右鍵膠囊 →「設定…」打開設定視窗(深色,左側選單):

| 頁面 | 內容 | 生效 |
|---|---|---|
| 一般 | 開機時啟動、閒置時隱藏、全螢幕時隱藏、切歌時顯示、音量亮度提示、快捷鍵(錄製) | 立即 |
| 外觀與位置 | 顯示在哪個螢幕(跟著主螢幕 / 指定螢幕)、與頂端距離、膠囊寬高、滑鼠移過去的寬度、展開寬高 | 立即(拖動滑桿時就套用) |
| | 軟體算圖 | 重新啟動 |
| 即時狀態 | 長條/小圓的優先順序(上下移動)、各模組要不要放進小圓 | 立即 |
| 模組 | 開關每個模組 | 重新啟動 |
| 天氣 | 目前位置、搜尋城市(Open-Meteo Geocoding,只送出輸入的地名)並選擇 | 立即(幾秒內更新) |
| Shelf | 截圖自動放進 Shelf | 立即 |

需要重新啟動的項目改了之後,底部會出現「重新啟動 Island」按鈕(先放掉單一執行個體 Mutex 再啟動新的行程)。模組可以實作 `IIslandSettingsPage` 加自己的設定頁,樣式用 `IslandTheme.Settings*` 資源鍵。

以下是 `settings.json` 的完整內容(設定視窗改的就是這個檔案,也可以手動改):

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
  "hotkey": "Ctrl+Alt+I",
  "announceActivity": true,
  "showOsd": true,
  "liveOrder": ["media", "timer", "privacy", "downloads", "battery", "weather", "shelf"],
  "liveHidden": [],
  "modules": ["clock", "weather", "timer", "media", "shelf", "osd", "battery"],
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
    Settings/
      SettingsWindow.xaml(.cs)   設定視窗(一般、外觀與位置、即時狀態、模組 + 模組自己的頁面)
      SettingsStyles.xaml        設定用的深色樣式(開關、按鈕、輸入框、卡片、滑桿、選項)
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
    IIslandSettingsPage.cs       選用:模組在設定視窗裡的自訂頁面
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
    Island.Modules.Links/
      LinksModule.cs             捷徑清單、開啟 / 切換、拖放加入、存檔
      WindowFinder.cs            列出應用程式視窗(Z 順序)、程式路徑、Store 程式 AUMID、檔案總管資料夾(Shell.Application)、切到前景
      LinksView.xaml(.cs)        捷徑分頁
      LinksSettingsView.cs       設定頁:目前的捷徑、從開著的視窗加入、瀏覽、網址
      LinkIcons.cs               圖示
    Island.Modules.Privacy/
      PrivacyModule.cs           ConsentStore\{microphone,webcam} 使用中偵測(RegNotifyChangeKeyValue)、提示一排、橘 / 綠點小圓
    Island.Modules.Downloads/
      DownloadsModule.cs         監看下載資料夾的暫存檔、大小、完成 → IIslandHost.SendToModule("shelf")
    Island.Modules.Battery/
      BatteryModule.cs           GetSystemPowerStatus + SystemEvents.PowerModeChanged(StatusChange):插拔、充飽、低電量
      BatteryViews.cs            提示的一排、電量環小圓(Segoe 電池圖示)
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
      ShelfItem.cs               一個項目(檔案 / 文字 / 網址 / 圖片;名稱、圖示、縮圖)
      ShelfClips.cs              純邏輯:文字或網址判斷、虛擬檔案檔名、shelf.json 新舊格式
      ScreenshotWatcher.cs       FileSystemWatcher 監看 Known Folder「Screenshots」,只收 20 秒內新建的圖片
      ScreenshotToastView.cs     截圖提示的一排(縮圖)
      ShelfOptions.cs            modules\shelf\options.json(captureScreenshots)
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

### 狀態機

島只有一個狀態欄位 `Surface`,五種值:

| 狀態 | 畫面 |
|---|---|
| `Hidden` | 看不見(只剩熱區);「閒置時隱藏」關閉時改用 `Idle` |
| `Idle` | 只有時間 + 小提示的小膠囊 |
| `Peek` | 滑鼠在島上:一排字 / 活動,旁邊可能有小圓 |
| `Announce` | 換歌、音量、計時到等提示(滑鼠沒碰) |
| `Expanded` | 展開的面板(點擊 = 固定,拖檔案 = 暫時) |

另外只有兩個附加資訊:`_pinned`(固定展開)、`_announced`(提示中的模組)。所有轉換都經過 `ShowSurface` / `HideSurface` / `ToExpanded` / `CollapseExpanded`,一排裡顯示什麼、小圓要不要出現都由 `UpdateContent` 依狀態推導。重構前用 6 個互相交錯的旗標,之前的時間殘影、選單關不掉都是旗標組合出的問題。

### 視窗大小跟著狀態

透明視窗(layered window)每畫一格都要把整個視窗送給系統,軟體算圖時視窗越大越耗 CPU。現在平常只用剛好放得下一排 + 兩側小圓的大小(125% 縮放時 535×75 實體像素,原本固定 1000×500),展開時才放大(543×270),收起動畫結束後再縮回來。一次音量提示(滑入 → 停留 → 滑出)實測 78~141 ms CPU,改版前同樣操作 141~688 ms。

### 錯誤處理

畫面執行緒的例外記錄後忽略;沒人等待的背景工作例外記錄並標記已處理;其他背景執行緒例外一定會讓 .NET 結束程式,至少先寫進 log。系統回呼(前景視窗事件、低階滑鼠掛勾、音量通知)內部都有 try/catch,不讓例外丟回原生程式碼。啟動時記錄各階段耗時(設定、模組、視窗);重複啟動被單一執行個體擋下時也會留紀錄。

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

## 麥克風 / 下載驗收(實機)

| 項目 | 結果 |
|---|---|
| 下載中(在下載資料夾寫一個會變大的 .crdownload) | ✅ 滑鼠移過去:左小圓「⬇ 3M」,長條仍是音樂 |
| 改名成正式檔名(= 下載完成) | ✅ 膠囊「✓ 下載完成 · island-download-test.zip · 5.0 MB」,檔案進 Shelf,之後自動隱藏 |
| 麥克風開始使用(在 ConsentStore 加一筆使用中的紀錄) | ✅ 膠囊「● 麥克風使用中 · MicTester」;滑鼠移過去左小圓是橘點 |
| 測試後清理 | ✅ 測試檔、登錄紀錄都已刪除 |

## 捷徑驗收(實機)

| 項目 | 結果 |
|---|---|
| 設定頁列出開著的視窗 | ✅ Claude、Chrome、Word、LINE、Spotify、檔案總管資料夾(顯示路徑) |
| 從開著的視窗加入 Store 程式 | ✅ Claude 存成 `shell:AppsFolder\Claude_…!Claude`(可在沒開時啟動) |
| 點 Chrome 捷徑 | ✅ 切到已開著的 Chrome 視窗 |
| 點 Downloads 捷徑 | ✅ 切到已開著的「下載 - 檔案總管」 |
| 點 Claude 捷徑 | ✅ 切回 Claude;點完島自動收起 |
| 拖檔案到島上 → 經過「捷徑」標籤 → 放開 | ✅ 自動切到捷徑分頁,加成捷徑(不是放進 Shelf) |

## 電量與截圖驗收(實機)

| 項目 | 結果 |
|---|---|
| 電量偵測 | ✅ log:100% 插電;充電到 99% 時滑鼠移過去出現綠色電量環「99」 |
| 插拔電源提示 | ✅ 使用者實際拔插充電線確認 |
| 截圖自動放進 Shelf | ✅ 在 `OneDrive\圖片\Screenshots` 放一張新圖 → 膠囊「縮圖 + 截圖已放進 Shelf」,約 3 秒後隱藏,Shelf 多 1 個 |
| 截圖檔被刪掉 | ✅ 打開 Shelf 分頁時自動從清單移除 |
| 閒置 60 秒(加入電量 + 截圖監看後,沒換歌) | 31 ms CPU、43 MB、18 執行緒 |

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
4. **Shelf 進階**:~~拖入文字/網址/圖片~~(已完成)、多選、較大的縮圖(`IShellItemImageFactory`)、選擇性複製到 Shelf 資料夾。
5. **設定畫面**:在右鍵選單加「設定」,選螢幕、開關模組,不用手動改 JSON。

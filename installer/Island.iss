; Island 安裝程式(Inno Setup 6)
; 不要直接編譯這個檔:執行 tools\build-installer.ps1,它會先發佈程式、產生精靈圖片,再帶入版本號編譯。
;
; - 只裝給目前的使用者:%LOCALAPPDATA%\Programs\Island,不需要系統管理員權限
; - 自帶 .NET 執行環境(self-contained),使用者不用另外安裝 .NET
; - 開始功能表捷徑、可選的桌面捷徑、可選的開機時啟動
; - 升級時自動關掉「這個資料夾裡」正在跑的 Island(不碰其他位置的 Island,例如開發中的版本)
; - 解除安裝時清掉開機啟動,並詢問要不要一併刪除設定(預設保留)

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\Island"
#endif
#ifndef AssetsDir
  #define AssetsDir "..\artifacts\installer-assets"
#endif

#define AppName "Island"
#define AppExe "Island.exe"

[Setup]
; AppId 固定不變,升級時才會蓋掉同一份安裝
AppId={{6E0B3C52-8F1D-4A7E-9C3B-2D5F1A9E7B40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=erich-5245
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Island 安裝程式
DefaultDirName={localappdata}\Programs\Island
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableDirPage=auto
DisableReadyPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts
OutputBaseFilename=Island-Setup-{#AppVersion}
SetupIconFile=..\src\Island.App\Assets\Island.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern dynamic
WizardImageFile={#AssetsDir}\wizard-100.bmp,{#AssetsDir}\wizard-200.bmp
WizardSmallImageFile={#AssetsDir}\wizard-small-100.bmp,{#AssetsDir}\wizard-small-200.bmp
WizardImageAlphaFormat=defined
; 深色模式另外讀這兩個;不設的話會換回 Inno 預設的圖
WizardImageFileDynamicDark={#AssetsDir}\wizard-100.bmp,{#AssetsDir}\wizard-200.bmp
WizardSmallImageFileDynamicDark={#AssetsDir}\wizard-small-100.bmp,{#AssetsDir}\wizard-small-200.bmp
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "zhtw"; MessagesFile: "compiler:Default.isl"

[Messages]
SetupAppTitle=安裝
SetupWindowTitle=安裝 %1
UninstallAppTitle=解除安裝
UninstallAppFullTitle=解除安裝 %1
ErrorTitle=錯誤
WindowsVersionNotSupported=Island 需要 Windows 10(2004 版)或更新的版本。
WinVersionTooLowError=%1 需要 %2 或更新的版本。
ExitSetupTitle=結束安裝
ExitSetupMessage=還沒裝完。現在結束的話,Island 不會被安裝。%n%n之後可以再執行一次安裝程式。%n%n要結束嗎?
ButtonBack=< 上一步(&B)
ButtonNext=下一步(&N) >
ButtonInstall=安裝(&I)
ButtonOK=確定
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonYesToAll=全部都是(&A)
ButtonNo=否(&N)
ButtonNoToAll=全部都否(&O)
ButtonFinish=完成(&F)
ButtonBrowse=瀏覽(&B)…
ButtonWizardBrowse=瀏覽(&R)…
ButtonNewFolder=新增資料夾(&M)
BrowseDialogTitle=選擇資料夾
BrowseDialogLabel=在下面選一個資料夾,再按「確定」。
NewFolderName=新資料夾
WelcomeLabel1=歡迎安裝 [name]
WelcomeLabel2=接下來會把 [name/ver] 安裝到這台電腦。%n%nIsland 會在螢幕上方中間放一座動態島:音樂、時間、捷徑、待辦都在這裡。只裝給你自己,不需要系統管理員權限。
WizardSelectDir=安裝位置
SelectDirDesc=要把 [name] 裝在哪裡?
SelectDirLabel3=[name] 會安裝到下面這個資料夾。
SelectDirBrowseLabel=按「下一步」繼續;想換位置就按「瀏覽」。
DiskSpaceGBLabel=需要至少 [gb] GB 的可用空間。
DiskSpaceMBLabel=需要至少 [mb] MB 的可用空間。
DirExistsTitle=資料夾已經存在
DirExists=這個資料夾:%n%n%1%n%n已經存在。還是要裝在這裡嗎?
WizardSelectTasks=其他選項
SelectTasksDesc=要順便做哪些事?
SelectTasksLabel2=勾選想要的項目,再按「下一步」。
WizardReady=準備安裝
ReadyLabel1=準備好把 [name] 安裝到這台電腦了。
ReadyLabel2a=按「安裝」開始;想改設定就按「上一步」。
ReadyLabel2b=按「安裝」開始。
ReadyMemoDir=安裝位置:
ReadyMemoTasks=其他選項:
WizardPreparing=準備中
PreparingDesc=正在準備安裝 [name]。
WizardInstalling=安裝中
InstallingLabel=正在安裝 [name],請稍候。
FinishedHeadingLabel=安裝完成
FinishedLabelNoIcons=[name] 已經安裝好了。
FinishedLabel=[name] 已經安裝好了,之後可以從開始功能表打開。
ClickFinish=按「完成」關閉安裝程式。
FinishedRestartLabel=要重新啟動電腦才能完成安裝,要現在重新啟動嗎?
RunEntryExec=執行 %1
SetupAborted=安裝沒有完成。%n%n請排除問題後再執行一次安裝程式。
StatusExtractFiles=正在解開檔案…
StatusCreateIcons=正在建立捷徑…
StatusCreateRegistryEntries=正在寫入登錄…
StatusRollback=正在還原變更…
UninstallOpenError=打不開「%1」,無法解除安裝。
ConfirmUninstall=確定要移除 %1 嗎?
UninstallStatusLabel=正在移除 %1,請稍候。
UninstalledAll=%1 已經移除。
UninstalledMost=%1 已經移除。%n%n有些檔案沒辦法刪掉,可以自己手動刪除。
UninstallDataCorrupted=「%1」已損毀,無法解除安裝。
WizardUninstalling=解除安裝
StatusUninstalling=正在移除 %1…

ClickNext=按「下一步」繼續,或按「取消」結束安裝。
ConfirmTitle=確認
SetupAlreadyRunning=安裝程式已經在執行了。
NotOnThisPlatform=這個程式不能在 %1 上執行。
OnlyOnTheseArchitectures=這個程式只能裝在下列處理器架構的 Windows:%n%n%1
SetupFileMissing=安裝資料夾裡少了 %1,請重新下載安裝程式。
SetupFileCorrupt=安裝檔已損毀,請重新下載。
InvalidPath=請輸入含磁碟機代號的完整路徑,例如:%n%nC:\APP
InvalidDrive=選的磁碟機不存在或無法使用,請換一個。
DiskSpaceWarningTitle=空間不夠
DiskSpaceWarning=安裝需要至少 %1 KB 的可用空間,但這個磁碟只剩 %2 KB。%n%n還是要繼續嗎?
DirNameTooLong=資料夾名稱或路徑太長。
InvalidDirName=資料夾名稱不正確。
BadDirName32=資料夾名稱不能包含這些字元:%n%n%1
DirDoesntExistTitle=資料夾不存在
DirDoesntExist=資料夾:%n%n%1%n%n不存在,要建立嗎?
ErrorCreatingDir=無法建立資料夾「%1」
StatusClosingApplications=正在關閉程式…
StatusSavingUninstall=正在儲存解除安裝資訊…
StatusRunProgram=正在完成安裝…
ErrorFunctionFailed=%1 失敗,代碼 %2
ErrorReplacingExistingFile=取代原有檔案時發生錯誤:
ErrorExtracting=解開檔案時發生錯誤:
UninstallNotFound=找不到「%1」,無法解除安裝。
UninstallAppRunningError=Island 還在執行,請先把它關掉,再按「確定」繼續。
SetupAppRunningError=Island 還在執行,請先把它關掉,再按「確定」繼續。
[CustomMessages]
AdditionalIcons=捷徑:
CreateDesktopIcon=在桌面建立捷徑(&D)
UninstallProgram=解除安裝 %1
LaunchProgram=開啟 %1
AutoStartTask=登入 Windows 後自動執行 Island(之後也可以在設定裡改)
StartupGroup=開機:
LaunchNow=現在開啟 Island
DeleteDataPrompt=要一併刪除 Island 的設定和資料嗎?%n(主題、捷徑、待辦、各模組選項,存在 %1)%n%n選「否」會保留,之後重新安裝時會沿用。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStartTask}"; GroupDescription: "{cm:StartupGroup}"

[InstallDelete]
; 升級時先清掉舊版的模組 dll:模組是掃描 Island.Modules.*.dll 載入的,新版拿掉的模組不能留著
Type: files; Name: "{app}\Island.Modules.*.dll"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; 和 Island 自己的「開機時啟動」用同一個值(HKCU\...\Run\Island),之後在設定裡開關會接著管
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Island"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

{ 關掉從「這個安裝資料夾」執行的 Island。其他位置的 Island(例如開發中的版本)不動。 }
procedure StopInstalledIsland(Dir: String);
var
  Code: Integer;
  Script: String;
begin
  StringChangeEx(Dir, '''', '''''', True);
  Script := 'Get-Process Island -ErrorAction SilentlyContinue | ' +
            'Where-Object { $_.Path -and $_.Path.StartsWith(''' + AddBackslash(Dir) + ''', [StringComparison]::OrdinalIgnoreCase) } | ' +
            'ForEach-Object { $_.Kill(); $_.WaitForExit(5000) | Out-Null }';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
       '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"',
       '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopInstalledIsland(ExpandConstant('{app}'));
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopInstalledIsland(ExpandConstant('{app}'));
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current, DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { 開機啟動指向這份安裝才刪;指向別的位置(例如開發版)就留著 }
    if RegQueryStringValue(HKCU, RunKey, 'Island', Current) and
       (Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Current)) > 0) then
      RegDeleteValue(HKCU, RunKey, 'Island');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\Island');
    if DirExists(DataDir) and not UninstallSilent() then
      if SuppressibleMsgBox(FmtMessage(CustomMessage('DeleteDataPrompt'), [DataDir]),
                            mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;

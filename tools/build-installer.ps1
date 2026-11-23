# 產生 Island 安裝程式:artifacts\Island-Setup-<版本>.exe
#
#   powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
#
# 步驟:跑測試 → 發佈自帶 .NET 的 win-x64 版本 → 用 Island 圖示畫安裝精靈的圖片 → Inno Setup 編譯。
# 需要 Inno Setup 6(winget install JRSoftware.InnoSetup --scope user)。版本號取自 Directory.Build.props 的 <Version>。
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish\Island'
$assets = Join-Path $artifacts 'installer-assets'

# ── 版本號 ──
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Encoding UTF8
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw '在 Directory.Build.props 找不到 <Version>' }
Write-Host "Island $version" -ForegroundColor Cyan

# ── Inno Setup ──
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw '找不到 Inno Setup 6。安裝:winget install JRSoftware.InnoSetup --scope user' }

# 開發中的 Island 開著會鎖住 bin 裡的 dll,建置會失敗
$running = Get-Process Island -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "先關掉從這個資料夾執行的 Island(PID $($running.Id -join ', ')),它會鎖住建置要覆蓋的檔案" }

# ── 測試 ──
if (-not $SkipTests) {
    dotnet test (Join-Path $root 'Island.sln') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw '測試沒有全部通過,不產生安裝程式' }
}

# ── 發佈(自帶 .NET,不打包成單一檔案:模組是在執行時掃描 Island.Modules.*.dll 載入的) ──
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'src\Island.App\Island.App.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o $publish --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失敗' }
$modules = @(Get-ChildItem $publish -Filter 'Island.Modules.*.dll')
if ($modules.Count -eq 0) { throw '發佈結果裡沒有任何模組 dll' }
$size = (Get-ChildItem $publish -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("發佈完成:{0} 個模組,{1:N0} MB" -f $modules.Count, $size)

# ── 安裝精靈的圖片(深色底 + Island 圖示,100% / 200% 兩種大小) ──
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $assets | Out-Null
$icoPath = Join-Path $root 'src\Island.App\Assets\Island.ico'

function New-WizardImage([int]$w, [int]$h, [string]$path, [bool]$transparent) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    if ($transparent) {
        $g.Clear([System.Drawing.Color]::Transparent)
        $iconSize = [int]($w * 0.86)
        $iconY = [int](($h - $iconSize) / 2)
    }
    else {
        $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
        $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(0x24, 0x24, 0x27)), ([System.Drawing.Color]::FromArgb(0x0B, 0x0B, 0x0C)), 90
        $g.FillRectangle($bg, $rect)
        # 上方的小膠囊 = 動態島
        $capW = [int]($w * 0.46); $capH = [int]($w * 0.12)
        $capX = [int](($w - $capW) / 2); $capY = [int]($h * 0.07)
        $cap = New-Object System.Drawing.Drawing2D.GraphicsPath
        $cap.AddArc($capX, $capY, $capH, $capH, 90, 180)
        $cap.AddArc($capX + $capW - $capH, $capY, $capH, $capH, 270, 180)
        $cap.CloseFigure()
        $g.FillPath([System.Drawing.Brushes]::Black, $cap)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0x50, 0xFF, 0xFF, 0xFF)), ([Math]::Max(1, $w / 164))
        $g.DrawPath($pen, $cap)
        $iconSize = [int]($w * 0.56)
        $iconY = [int]($h * 0.36)
    }
    $src = Get-LargestIconFrame
    $g.DrawImage($src, [int](($w - $iconSize) / 2), $iconY, $iconSize, $iconSize)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose(); $src.Dispose()
}

# .ico 裡 256px 那張是 PNG 壓縮的,System.Drawing.Icon 讀不了;改用 WPF 的解碼器取最大那張
function Get-LargestIconFrame {
    Add-Type -AssemblyName PresentationCore
    $stream = [IO.File]::OpenRead($icoPath)
    try {
        $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create($stream, 'PreservePixelFormat', 'OnLoad')
        $frame = $decoder.Frames | Sort-Object PixelWidth -Descending | Select-Object -First 1
        $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($frame))
        $ms = New-Object IO.MemoryStream
        $enc.Save($ms)
        $ms.Position = 0
        return [System.Drawing.Bitmap]::FromStream($ms)
    }
    finally { $stream.Dispose() }
}

New-WizardImage 164 314 (Join-Path $assets 'wizard-100.bmp') $false
New-WizardImage 328 628 (Join-Path $assets 'wizard-200.bmp') $false
New-WizardImage 55 55 (Join-Path $assets 'wizard-small-100.bmp') $true
New-WizardImage 110 110 (Join-Path $assets 'wizard-small-200.bmp') $true

# ── 編譯安裝程式 ──
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publish" "/DAssetsDir=$assets" (Join-Path $root 'installer\Island.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup 編譯失敗' }

$setup = Join-Path $artifacts "Island-Setup-$version.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
Set-Content -Path "$setup.sha256" -Value "$hash  Island-Setup-$version.exe" -Encoding ascii
Write-Host ("完成:{0}({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green
Write-Host "SHA-256:$hash"

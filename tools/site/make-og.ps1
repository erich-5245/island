# Render tools/site/og.html to site/og.png (1200x630 link preview) with headless Edge.
# Usage:  powershell -ExecutionPolicy Bypass -File tools\site\make-og.ps1
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$edge = @(
  "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
  "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge not found' }

$src = (Join-Path $PSScriptRoot 'og.html') -replace '\\', '/'
$out = Join-Path $root 'site\og.png'
$profile = Join-Path $env:TEMP 'island-og-profile'
& $edge --headless=new --disable-gpu --hide-scrollbars --user-data-dir="$profile" `
  --window-size=1200,630 --virtual-time-budget=8000 --screenshot="$out" "file:///$src" | Out-Null
Start-Sleep -Milliseconds 500
if (-not (Test-Path $out)) { throw "screenshot failed: $out" }
"wrote $out ($((Get-Item $out).Length) bytes)"

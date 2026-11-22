# Generate src/Island.App/Assets/Island.ico (and a 512 px preview PNG) from AtollIcons.cs.
# Every size is drawn separately (32 px and below use a simplified drawing) and stored as PNG inside the .ico.
# Usage:  powershell -ExecutionPolicy Bypass -File tools\app-icon\make-icon.ps1
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'AtollIcons.cs') -ReferencedAssemblies System.Drawing
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$assets = Join-Path $root 'src\Island.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
$palette = [AtollIcons]::Navy()
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256

$images = foreach ($s in $sizes) {
  $bmp = [AtollIcons]::Draw($s, $palette)
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  ,@($s, $ms.ToArray())
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$images.Count)   # ICONDIR
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {                                                     # ICONDIRENTRY
  $s = $img[0]; $len = $img[1].Length
  $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $w.Write([byte]0); $w.Write([byte]0); $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]$len); $w.Write([UInt32]$offset); $offset += $len
}
foreach ($img in $images) { $w.Write($img[1]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'Island.ico'), $out.ToArray())
$preview = [AtollIcons]::Draw(512, $palette); $preview.Save((Join-Path $assets 'Island-512.png')); $preview.Dispose()
"wrote $(Join-Path $assets 'Island.ico') ($($out.Length) bytes, sizes: $($sizes -join ', '))"

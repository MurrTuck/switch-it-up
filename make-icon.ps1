# Draws the app icon (gradient tile with two swap arrows) and saves it as app.ico.
# Optional: -PreviewPath some.png also saves the 256px version as a PNG.
param([string]$PreviewPath)
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 112
    $path.AddArc(8, 8, $d, $d, 180, 90)
    $path.AddArc(248 - $d, 8, $d, $d, 270, 90)
    $path.AddArc(248 - $d, 248 - $d, $d, $d, 0, 90)
    $path.AddArc(8, 248 - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.Rectangle 0, 0, 256, 256
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(124, 92, 255)), ([System.Drawing.Color]::FromArgb(0, 194, 224)), 35.0
    $g.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 22
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $white = [System.Drawing.Brushes]::White
    # top arrow, pointing right
    $g.DrawLine($pen, 66, 100, 160, 100)
    $g.FillPolygon($white, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 154, 62), (New-Object System.Drawing.Point 204, 100), (New-Object System.Drawing.Point 154, 138)))
    # bottom arrow, pointing left
    $g.DrawLine($pen, 190, 158, 96, 158)
    $g.FillPolygon($white, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 102, 120), (New-Object System.Drawing.Point 52, 158), (New-Object System.Drawing.Point 102, 196)))

    $g.Dispose()
    return $bmp
}

# 256px frames are stored as PNG; smaller ones as classic 32-bit bitmaps (widest compatibility).
function Get-FrameBytes($bmp) {
    $size = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$ms.ToArray()
    }
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($size * $size * 4)
    for ($y = 0; $y -lt $size; $y++) {
        # bitmap rows are stored bottom-up
        $src = [IntPtr]($data.Scan0.ToInt64() + ($size - 1 - $y) * $data.Stride)
        [System.Runtime.InteropServices.Marshal]::Copy($src, $pixels, $y * $size * 4, $size * 4)
    }
    $bmp.UnlockBits($data)
    $mask = New-Object byte[] ([int]([Math]::Floor(($size + 31) / 32)) * 4 * $size)
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([uint32]40); $w.Write([int32]$size); $w.Write([int32]($size * 2))
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]0)
    $w.Write([uint32]($pixels.Length + $mask.Length))
    $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    $w.Write($pixels); $w.Write($mask); $w.Flush()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 256
$frames = @{}
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -eq 256 -and $PreviewPath) { $bmp.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png) }
    $frames[$s] = Get-FrameBytes $bmp
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$s].Length); $w.Write([uint32]$offset)
    $offset += $frames[$s].Length
}
foreach ($s in $sizes) { $w.Write($frames[$s]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'app.ico'), $out.ToArray())

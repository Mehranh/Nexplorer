# Renders the Nexplorer logo programmatically using System.Drawing,
# producing logo.png (1024) and nexplorer.ico (multi-size, PNG-encoded).
# Run from repo root: pwsh ./scripts/build-logo.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $repoRoot 'src\Nexplorer.App'
$pngOut   = Join-Path $assetDir 'logo.png'
$icoOut   = Join-Path $assetDir 'nexplorer.ico'

function New-RoundedRectPath {
    param([float]$x, [float]$y, [float]$w, [float]$h, [float]$r)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2.0
    if ($d -le 0 -or $d -gt [Math]::Min($w, $h)) {
        $path.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $w, $h))
        return $path
    }
    $path.AddArc($x,             $y,             $d, $d, 180, 90)
    $path.AddArc($x + $w - $d,   $y,             $d, $d, 270, 90)
    $path.AddArc($x + $w - $d,   $y + $h - $d,   $d, $d,   0, 90)
    $path.AddArc($x,             $y + $h - $d,   $d, $d,  90, 90)
    $path.CloseFigure()
    return $path
}

function New-NexplorerLogo {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality= [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Design coordinates assume a 1024 canvas; scale down for smaller bitmaps.
    $s = [float]($Size / 1024.0)

    # ---- Rounded-square background ----
    $bgRadius = 224.0 * $s
    $bgPath = New-RoundedRectPath 0 0 $Size $Size $bgRadius
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0),
        (New-Object System.Drawing.PointF ([float]$Size), ([float]$Size)),
        [System.Drawing.Color]::FromArgb(255, 0x12, 0x1E, 0x40),
        [System.Drawing.Color]::FromArgb(255, 0x05, 0x0A, 0x1C)
    )
    $g.FillPath($bgBrush, $bgPath)
    $bgBrush.Dispose()
    $bgPath.Dispose()

    # ---- N foreground (single gradient brush shared by all three shapes) ----
    $nBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF ([float](288 * $s)), ([float](256 * $s))),
        (New-Object System.Drawing.PointF ([float](736 * $s)), ([float](768 * $s))),
        [System.Drawing.Color]::FromArgb(255, 0x34, 0xD3, 0xEE),
        [System.Drawing.Color]::FromArgb(255, 0xA7, 0x8B, 0xFA)
    )
    # Three-stop blend (cyan -> periwinkle -> violet)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors    = @(
        [System.Drawing.Color]::FromArgb(255, 0x34, 0xD3, 0xEE),
        [System.Drawing.Color]::FromArgb(255, 0x7A, 0xA2, 0xF7),
        [System.Drawing.Color]::FromArgb(255, 0xA7, 0x8B, 0xFA)
    )
    $blend.Positions = @([single]0.0, [single]0.55, [single]1.0)
    $nBrush.InterpolationColors = $blend

    # Bar geometry
    $barRadius = 28.0 * $s
    $leftBar  = New-RoundedRectPath ([float](288 * $s)) ([float](256 * $s)) ([float](128 * $s)) ([float](512 * $s)) $barRadius
    $rightBar = New-RoundedRectPath ([float](608 * $s)) ([float](256 * $s)) ([float](128 * $s)) ([float](512 * $s)) $barRadius

    # Diagonal slab — parallelogram from top of left bar to bottom of right bar
    $slab = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ([float](356 * $s)), ([float](256 * $s))),
        (New-Object System.Drawing.PointF ([float](476 * $s)), ([float](256 * $s))),
        (New-Object System.Drawing.PointF ([float](668 * $s)), ([float](768 * $s))),
        (New-Object System.Drawing.PointF ([float](548 * $s)), ([float](768 * $s)))
    )
    $slab.AddPolygon($points)

    $g.FillPath($nBrush, $leftBar)
    $g.FillPath($nBrush, $rightBar)
    $g.FillPath($nBrush, $slab)

    $leftBar.Dispose(); $rightBar.Dispose(); $slab.Dispose(); $nBrush.Dispose()
    $g.Dispose()
    return $bmp
}

# ---- Render master PNG ----
Write-Host "Rendering logo.png (1024x1024)..."
$master = New-NexplorerLogo -Size 1024
$master.Save($pngOut, [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()
Write-Host "  -> $pngOut"

# ---- Build multi-size ICO (PNG-encoded entries, Vista+ format) ----
Write-Host "Building nexplorer.ico (multi-size)..."
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = foreach ($sz in $sizes) {
    $bmp = New-NexplorerLogo -Size $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [PSCustomObject]@{ Size = $sz; Bytes = $ms.ToArray() }
    $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

# ICONDIR
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type = ICO
$bw.Write([uint16]$entries.Count) # count

$dataOffset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $w = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
    $h = $w
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)            # palette colors (0 = no palette)
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # planes
    $bw.Write([uint16]32)         # bit count
    $bw.Write([uint32]$e.Bytes.Length)
    $bw.Write([uint32]$dataOffset)
    $dataOffset += $e.Bytes.Length
}
foreach ($e in $entries) { $bw.Write($e.Bytes) }

[System.IO.File]::WriteAllBytes($icoOut, $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Host "  -> $icoOut ($($entries.Count) sizes)"
Write-Host "Done."

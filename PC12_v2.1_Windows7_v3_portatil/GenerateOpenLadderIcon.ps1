$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class OpenLadderNativeIcon {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$rect = New-Object System.Drawing.RectangleF(4, 4, 248, 248)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 16, 72, 112), [System.Drawing.Color]::FromArgb(255, 4, 18, 32), 90.0)
$outer = New-RoundedRectPath 4 4 248 248 30
$g.FillPath($bg, $outer)
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 23, 157, 232), 4)
$g.DrawPath($borderPen, $outer)

$whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 6)
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$bluePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 0, 157, 232), 3)
$bluePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$bluePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

# Trilhos Ladder
$g.DrawLine($whitePen, 28, 28, 28, 128)
$g.DrawLine($whitePen, 228, 28, 228, 128)
$g.DrawLine($whitePen, 28, 72, 64, 72)
$g.DrawLine($whitePen, 76, 54, 76, 90)
$g.DrawLine($whitePen, 91, 54, 91, 90)
$g.DrawLine($whitePen, 91, 72, 112, 72)

# Bloco PLC central
$plcPath = New-RoundedRectPath 112 49 64 46 10
$plcPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 5)
$g.DrawPath($plcPen, $plcPath)
$plcFont = New-Object System.Drawing.Font('Segoe UI', 18, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$plcBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$plcText = 'PLC'
$plcSize = $g.MeasureString($plcText, $plcFont)
$g.DrawString($plcText, $plcFont, $plcBrush, 144 - ($plcSize.Width / 2), 62)
$g.DrawLine($whitePen, 176, 72, 192, 72)

# Bobina Ladder
$g.DrawArc($whitePen, 188, 53, 29, 38, 105, 150)
$g.DrawArc($whitePen, 207, 53, 29, 38, 285, 150)
$g.DrawLine($whitePen, 224, 72, 228, 72)

# Trilhas eletrônicas em azul
$g.DrawLine($bluePen, 28, 100, 70, 100)
$g.DrawLine($bluePen, 70, 100, 82, 112)
$g.DrawLine($bluePen, 82, 112, 126, 112)
$g.DrawLine($bluePen, 126, 112, 140, 100)
$g.DrawLine($bluePen, 140, 100, 174, 100)
$g.DrawLine($bluePen, 174, 100, 184, 90)
$g.DrawLine($bluePen, 184, 90, 203, 90)
$g.DrawEllipse($bluePen, 67, 96, 8, 8)
$g.DrawEllipse($bluePen, 199, 86, 8, 8)

# Nome do produto
$openFont = New-Object System.Drawing.Font('Segoe UI', 34, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$ladderFont = New-Object System.Drawing.Font('Segoe UI', 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$studioFont = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$cyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 174, 239))

function Draw-CenteredText($graphics, [string]$text, $font, $brush, [float]$y) {
    $s = $graphics.MeasureString($text, $font)
    $graphics.DrawString($text, $font, $brush, ($size - $s.Width) / 2, $y)
}

Draw-CenteredText $g 'OPEN' $openFont $whiteBrush 128
Draw-CenteredText $g 'LADDER' $ladderFont $cyanBrush 164
Draw-CenteredText $g 'S T U D I O' $studioFont $whiteBrush 208
$g.DrawLine($bluePen, 52, 220, 79, 220)
$g.DrawLine($bluePen, 177, 220, 204, 220)

$pngPath = Join-Path (Get-Location) 'OpenLadderStudio.icon.png'
$icoPath = Join-Path (Get-Location) 'OpenLadderStudio.ico'
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$hIcon = $bmp.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
    try { $icon.Save($fs) } finally { $fs.Dispose() }
} finally {
    [OpenLadderNativeIcon]::DestroyIcon($hIcon) | Out-Null
}

$bluePen.Dispose(); $whitePen.Dispose(); $borderPen.Dispose(); $plcPen.Dispose()
$bg.Dispose(); $outer.Dispose(); $plcPath.Dispose()
$plcFont.Dispose(); $openFont.Dispose(); $ladderFont.Dispose(); $studioFont.Dispose()
$plcBrush.Dispose(); $whiteBrush.Dispose(); $cyanBrush.Dispose()
$g.Dispose(); $bmp.Dispose()

if (-not (Test-Path $icoPath)) { throw 'Falha ao gerar OpenLadderStudio.ico.' }

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

# Fundo preserva a identidade visual do ícone anterior, agora sem texto.
$rect = New-Object System.Drawing.RectangleF(4, 4, 248, 248)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 16, 72, 112),
    [System.Drawing.Color]::FromArgb(255, 4, 18, 32),
    90.0)
$outer = New-RoundedRectPath 4 4 248 248 30
$g.FillPath($bg, $outer)
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 23, 157, 232), 4)
$g.DrawPath($borderPen, $outer)

$whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 7)
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$bluePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(225, 0, 157, 232), 3)
$bluePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$bluePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

# Ladder central ampliado para permanecer legível em 16x16, 32x32 e 48x48.
$leftRail = 30
$rightRail = 226
$centerY = 112
$g.DrawLine($whitePen, $leftRail, 42, $leftRail, 192)
$g.DrawLine($whitePen, $rightRail, 42, $rightRail, 192)
$g.DrawLine($whitePen, $leftRail, $centerY, 68, $centerY)

# Contato normalmente aberto.
$g.DrawLine($whitePen, 72, 86, 72, 138)
$g.DrawLine($whitePen, 90, 86, 90, 138)
$g.DrawLine($whitePen, 90, $centerY, 112, $centerY)

# Bloco PLC sem texto: o símbolo fica reconhecível sem repetir o nome do software.
$plcPath = New-RoundedRectPath 112 83 64 58 10
$plcPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 6)
$g.DrawPath($plcPen, $plcPath)
$g.DrawLine($whitePen, 176, $centerY, 188, $centerY)

# Bobina Ladder.
$g.DrawArc($whitePen, 184, 91, 29, 42, 105, 150)
$g.DrawArc($whitePen, 203, 91, 29, 42, 285, 150)
$g.DrawLine($whitePen, 222, $centerY, $rightRail, $centerY)

# Trilhas eletrônicas inspiradas no ícone anterior.
$g.DrawLine($bluePen, 30, 154, 72, 154)
$g.DrawLine($bluePen, 72, 154, 84, 166)
$g.DrawLine($bluePen, 84, 166, 128, 166)
$g.DrawLine($bluePen, 128, 166, 142, 154)
$g.DrawLine($bluePen, 142, 154, 176, 154)
$g.DrawLine($bluePen, 176, 154, 188, 142)
$g.DrawLine($bluePen, 188, 142, 208, 142)

$g.DrawLine($bluePen, 30, 174, 70, 174)
$g.DrawLine($bluePen, 70, 174, 88, 190)
$g.DrawLine($bluePen, 88, 190, 136, 190)
$g.DrawLine($bluePen, 136, 190, 154, 176)
$g.DrawLine($bluePen, 154, 176, 204, 176)

$g.DrawLine($bluePen, 30, 66, 72, 66)
$g.DrawLine($bluePen, 72, 66, 84, 54)
$g.DrawLine($bluePen, 84, 54, 150, 54)
$g.DrawLine($bluePen, 150, 54, 164, 68)
$g.DrawLine($bluePen, 164, 68, 202, 68)

$g.DrawEllipse($bluePen, 68, 150, 8, 8)
$g.DrawEllipse($bluePen, 204, 138, 8, 8)
$g.DrawEllipse($bluePen, 80, 50, 8, 8)
$g.DrawEllipse($bluePen, 198, 64, 8, 8)

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
$g.Dispose(); $bmp.Dispose()

if (-not (Test-Path $icoPath)) { throw 'Falha ao gerar OpenLadderStudio.ico.' }

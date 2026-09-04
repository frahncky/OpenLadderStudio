$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

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

function New-OpenLadderBitmap([int]$size) {
    $scale = $size / 256.0
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $outer = New-RoundedRectPath (4*$scale) (4*$scale) (248*$scale) (248*$scale) (34*$scale)
    $bgRect = New-Object System.Drawing.RectangleF((4*$scale), (4*$scale), (248*$scale), (248*$scale))
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bgRect,
        [System.Drawing.Color]::FromArgb(255, 28, 34, 40),
        [System.Drawing.Color]::FromArgb(255, 12, 16, 21),
        90.0)
    $g.FillPath($bg, $outer)

    $borderWidth = [Math]::Max(1.0, 3.0 * $scale)
    $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 62, 72, 82), $borderWidth)
    $g.DrawPath($border, $outer)

    $green = [System.Drawing.Color]::FromArgb(255, 45, 170, 107)
    $greenLight = [System.Drawing.Color]::FromArgb(255, 78, 201, 176)
    $blue = [System.Drawing.Color]::FromArgb(255, 91, 170, 245)
    $white = [System.Drawing.Color]::FromArgb(255, 238, 242, 246)
    $panel = [System.Drawing.Color]::FromArgb(255, 39, 46, 54)

    $railPen = New-Object System.Drawing.Pen($green, [Math]::Max(1.4, 7.0 * $scale))
    $railPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $railPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $linePen = New-Object System.Drawing.Pen($white, [Math]::Max(1.2, 6.0 * $scale))
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $accentPen = New-Object System.Drawing.Pen($greenLight, [Math]::Max(1.0, 3.5 * $scale))
    $accentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $accentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $bluePen = New-Object System.Drawing.Pen($blue, [Math]::Max(1.0, 3.5 * $scale))
    $bluePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $bluePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Trilhos Ladder: a cor verde é a assinatura visual principal do OpenLadder Studio.
    $g.DrawLine($railPen, 42*$scale, 48*$scale, 42*$scale, 208*$scale)
    $g.DrawLine($railPen, 214*$scale, 48*$scale, 214*$scale, 208*$scale)

    # Rung principal: contato -> bloco PLC -> bobina.
    $y = 112 * $scale
    $g.DrawLine($linePen, 42*$scale, $y, 78*$scale, $y)
    $g.DrawLine($linePen, 84*$scale, 88*$scale, 84*$scale, 136*$scale)
    $g.DrawLine($linePen, 104*$scale, 88*$scale, 104*$scale, 136*$scale)
    $g.DrawLine($linePen, 104*$scale, $y, 124*$scale, $y)

    $plcPath = New-RoundedRectPath (124*$scale) (80*$scale) (52*$scale) (64*$scale) (9*$scale)
    $panelBrush = New-Object System.Drawing.SolidBrush($panel)
    $g.FillPath($panelBrush, $plcPath)
    $g.DrawPath($bluePen, $plcPath)
    $g.DrawLine($bluePen, 137*$scale, 96*$scale, 163*$scale, 96*$scale)
    $g.DrawLine($bluePen, 137*$scale, 108*$scale, 156*$scale, 108*$scale)
    $dotBrush = New-Object System.Drawing.SolidBrush($greenLight)
    $g.FillEllipse($dotBrush, 138*$scale, 122*$scale, 6*$scale, 6*$scale)
    $g.FillEllipse($dotBrush, 150*$scale, 122*$scale, 6*$scale, 6*$scale)
    $g.FillEllipse($dotBrush, 162*$scale, 122*$scale, 6*$scale, 6*$scale)

    $g.DrawLine($linePen, 176*$scale, $y, 184*$scale, $y)
    $g.DrawArc($linePen, 180*$scale, 92*$scale, 22*$scale, 40*$scale, 105, 150)
    $g.DrawArc($linePen, 194*$scale, 92*$scale, 22*$scale, 40*$scale, 285, 150)

    # Rung secundário simplificado: reforça a leitura do ícone em tamanhos pequenos.
    $lowerY = 168 * $scale
    $g.DrawLine($accentPen, 42*$scale, $lowerY, 82*$scale, $lowerY)
    $g.DrawLine($accentPen, 82*$scale, $lowerY, 96*$scale, 154*$scale)
    $g.DrawLine($accentPen, 96*$scale, 154*$scale, 146*$scale, 154*$scale)
    $g.DrawLine($accentPen, 146*$scale, 154*$scale, 160*$scale, $lowerY)
    $g.DrawLine($accentPen, 160*$scale, $lowerY, 214*$scale, $lowerY)

    $dotSize = [Math]::Max(2.0, 7.0 * $scale)
    $g.FillEllipse($dotBrush, (78*$scale), ($lowerY - $dotSize/2), $dotSize, $dotSize)
    $g.FillEllipse($dotBrush, (156*$scale), ($lowerY - $dotSize/2), $dotSize, $dotSize)

    $dotBrush.Dispose(); $panelBrush.Dispose(); $bluePen.Dispose(); $accentPen.Dispose();
    $linePen.Dispose(); $railPen.Dispose(); $border.Dispose(); $bg.Dispose();
    $plcPath.Dispose(); $outer.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $ms = New-Object System.IO.MemoryStream
    try {
        $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }
    finally {
        $ms.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = New-OpenLadderBitmap $size
    try {
        $frames += New-Object PSObject -Property @{ Size = $size; Bytes = (Get-PngBytes $bitmap) }
        if ($size -eq 256) {
            $bitmap.Save((Join-Path (Get-Location) 'OpenLadderStudio.icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$icoPath = Join-Path (Get-Location) 'OpenLadderStudio.ico'
$stream = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([UInt16]0)                 # reserved
    $writer.Write([UInt16]1)                 # icon
    $writer.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $sizeByte = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([Byte]$sizeByte)
        $writer.Write([Byte]$sizeByte)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

if (-not (Test-Path $icoPath)) { throw 'Falha ao gerar OpenLadderStudio.ico.' }
if ((Get-Item $icoPath).Length -lt 1024) { throw 'OpenLadderStudio.ico foi gerado com tamanho inválido.' }

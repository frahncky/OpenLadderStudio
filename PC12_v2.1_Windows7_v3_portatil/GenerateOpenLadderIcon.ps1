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

    # Fundo simples e contrastante: permanece legivel nos tamanhos 16/24/32 px.
    $outer = New-RoundedRectPath (5*$scale) (5*$scale) (246*$scale) (246*$scale) (38*$scale)
    $bgRect = New-Object System.Drawing.RectangleF((5*$scale), (5*$scale), (246*$scale), (246*$scale))
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bgRect,
        [System.Drawing.Color]::FromArgb(255, 16, 58, 82),
        [System.Drawing.Color]::FromArgb(255, 8, 30, 45),
        90.0)
    $g.FillPath($bg, $outer)

    $border = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(255, 47, 96, 126),
        [Math]::Max(1.0, 3.0*$scale))
    $g.DrawPath($border, $outer)

    $white = [System.Drawing.Color]::FromArgb(255, 241, 245, 249)
    $soft = [System.Drawing.Color]::FromArgb(255, 191, 219, 235)
    $amber = [System.Drawing.Color]::FromArgb(255, 255, 194, 71)
    $green = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)

    # A marca combina uma pequena escada Ladder com um L e um O.
    # Poucos elementos, traços grossos e nenhuma microinformacao decorativa.
    $mainW = [Math]::Max(1.5, 9.0*$scale)
    $secondaryW = [Math]::Max(1.1, 6.0*$scale)
    $whitePen = New-Object System.Drawing.Pen($white, $mainW)
    $whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $softPen = New-Object System.Drawing.Pen($soft, $secondaryW)
    $softPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $softPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $amberPen = New-Object System.Drawing.Pen($amber, [Math]::Max(1.5, 10.0*$scale))
    $amberPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $amberPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Ladder: dois trilhos e tres degraus.
    $g.DrawLine($whitePen, 55*$scale, 48*$scale, 55*$scale, 207*$scale)
    $g.DrawLine($whitePen, 101*$scale, 48*$scale, 101*$scale, 207*$scale)
    $g.DrawLine($softPen, 55*$scale, 76*$scale, 101*$scale, 76*$scale)
    $g.DrawLine($softPen, 55*$scale, 128*$scale, 101*$scale, 128*$scale)
    $g.DrawLine($softPen, 55*$scale, 180*$scale, 101*$scale, 180*$scale)

    # O em ambar: ponto focal e acento da identidade.
    $g.DrawEllipse($amberPen, 132*$scale, 51*$scale, 66*$scale, 66*$scale)

    # L integrado ao lado direito: haste + base.
    $g.DrawLine($whitePen, 135*$scale, 139*$scale, 135*$scale, 196*$scale)
    $g.DrawLine($whitePen, 135*$scale, 196*$scale, 199*$scale, 196*$scale)

    # Um unico estado verde, grande o bastante para nao virar ruido em 16 px.
    $statusBrush = New-Object System.Drawing.SolidBrush($green)
    $dot = [Math]::Max(3.0, 13.0*$scale)
    $g.FillEllipse($statusBrush, 186*$scale, 137*$scale, $dot, $dot)

    $statusBrush.Dispose()
    $amberPen.Dispose(); $softPen.Dispose(); $whitePen.Dispose()
    $border.Dispose(); $bg.Dispose(); $outer.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $ms = New-Object System.IO.MemoryStream
    try {
        $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,([byte[]]$ms.ToArray())
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
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
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
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

if (-not (Test-Path $icoPath)) { throw 'Falha ao gerar OpenLadderStudio.ico.' }
if ((Get-Item $icoPath).Length -lt 1024) { throw 'OpenLadderStudio.ico foi gerado com tamanho inválido.' }

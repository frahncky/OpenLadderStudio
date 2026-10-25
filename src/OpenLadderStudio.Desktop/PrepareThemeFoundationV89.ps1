$ErrorActionPreference = 'Stop'
# V89 - Fundacao visual: superficie uniforme no canvas Ladder e cores de estado.
# Roda depois do V88, no fim da cadeia. A paleta nova (AppBranding.cs) ganhou
# CanvasRow, CanvasHover e Energized; aqui o editor passa a usa-los.
#
# Antes: a linha selecionada era pintada com Chrome e as demais com Canvas, e o
# fundo "limpo" por tras de cada simbolo seguia o mesmo criterio. No tema claro
# Chrome e Canvas eram brancos diferentes, entao celulas vizinhas alternavam
# branco e cinza sem regra. Agora: Canvas em tudo, CanvasRow so na linha
# selecionada, CanvasHover so sob o cursor, e o simbolo apaga o fio com a cor
# exata do que esta atras dele.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
$modbusPath = Join-Path $root 'ModbusMonitorV18.build.cs'
if (-not (Test-Path -LiteralPath $ladderPath)) { throw 'V89: LadderEditor.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $modbusPath)) { throw 'V89: ModbusMonitorV18.build.cs nao encontrado.' }

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    # O arquivo gerado pode vir com CRLF ou LF; o texto trabalhado usa LF.
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    if (-not $text.Contains($old)) { throw "V89: ancora nao encontrada ($label). Ajuste PrepareThemeFoundationV89.ps1." }
    return $text.Replace($old, $new)
}

function Read-Lf([string]$path) {
    $raw = [IO.File]::ReadAllText($path)
    return @{ Text = $raw.Replace("`r`n", "`n"); Crlf = $raw.Contains("`r`n") }
}

function Write-Back([string]$path, [string]$text, [bool]$crlf) {
    if ($crlf) { $text = $text.Replace("`n", "`r`n") }
    [IO.File]::WriteAllText($path, $text, [Text.Encoding]::UTF8)
}

# ---------------------------------------------------------------------------
# 1. Canvas do editor.
# ---------------------------------------------------------------------------
$file = Read-Lf $ladderPath
$ladder = $file.Text

if ($ladder.Contains('OpenLadderPalette.CanvasRow')) {
    Write-Host 'OpenLadder Studio: fundacao visual V89 ja aplicada no editor.' -ForegroundColor DarkGray
}
else {
    # A coluna de saida deixa de ter sombra propria: o cabecalho C8 ja a identifica,
    # e a sombra era uma das superficies que alternavam com o fundo.
    $ladder = Rep $ladder 'using (Brush outputShade = new SolidBrush(OpenLadderPalette.Chrome))' 'using (Brush outputShade = new SolidBrush(OpenLadderPalette.Canvas))' 'sombra da coluna de saida'

    $ladder = Rep $ladder 'using (Brush row = new SolidBrush(OpenLadderPalette.Chrome))' 'using (Brush row = new SolidBrush(OpenLadderPalette.CanvasRow))' 'fundo da linha selecionada'

    $ladder = Rep $ladder 'using (Brush hover = new SolidBrush(OpenLadderPalette.NavHover)) g.FillRectangle(hover, box);' 'using (Brush hover = new SolidBrush(OpenLadderPalette.CanvasHover)) g.FillRectangle(hover, box);' 'preenchimento do hover'

    $ladder = Rep $ladder '            if (!branch && rung == HoverRung && column == HoverColumn)
                return OpenLadderPalette.NavHover;
            return rung == SelectedRung || column == LadderRung.ColumnCount - 1
                ? OpenLadderPalette.Chrome : OpenLadderPalette.Canvas;' '            if (!branch && rung == HoverRung && column == HoverColumn)
                return OpenLadderPalette.CanvasHover;
            return rung == SelectedRung ? OpenLadderPalette.CanvasRow : OpenLadderPalette.Canvas;' 'fundo por tras do simbolo'

    $ladder = Rep $ladder 'new Pen(rungOn ? OpenLadderPalette.Ok : OpenLadderPalette.Wire, rungOn ? 2.8f : 2.0f)' 'new Pen(rungOn ? OpenLadderPalette.Energized : OpenLadderPalette.Wire, rungOn ? 2.8f : 2.0f)' 'fio energizado'

    Write-Back $ladderPath $ladder $file.Crlf
    Write-Host 'OpenLadder Studio: fundacao visual V89 aplicada (canvas uniforme, linha selecionada sutil, fio energizado).' -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# 2. Monitor Modbus: o botao "parar on-line" trazia dois vermelhos fixos.
# ---------------------------------------------------------------------------
foreach ($name in 'ModbusMonitorV17.build.cs', 'ModbusMonitorV18.build.cs') {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $m = Read-Lf $path
    $text = $m.Text
    if (-not $text.Contains('Color.FromArgb(153, 75, 75)')) { continue }
    $text = Rep $text 'onlineEnabled ? Color.FromArgb(153, 75, 75) : PanelColor' 'onlineEnabled ? OpenLadderPalette.DangerBg : PanelColor' "botao on-line ($name)"
    $text = Rep $text 'onlineEnabled ? Color.FromArgb(180, 92, 92) : Border' 'onlineEnabled ? OpenLadderPalette.Danger : Border' "borda do botao on-line ($name)"
    Write-Back $path $text $m.Crlf
}

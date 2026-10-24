$ErrorActionPreference = 'Stop'
# V86 - Simbolos Ladder IEC 61131-3 no canvas do editor.
# Roda depois do V85 (ultimo passo antes da compilacao). O desenho dos simbolos
# vive em LadderSymbols.cs; aqui o canvas passa a delega-lo, ganha o estado de
# energizacao por linha (SetRungPower) e o cabecalho C1..C8 do guia de interface.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$path = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'V86: LadderEditor.build.cs nao encontrado.' }

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    # O script pode vir com CRLF (checkout no Windows); o texto trabalhado usa LF.
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    if (-not $text.Contains($old)) { throw "V86: ancora nao encontrada ($label). Ajuste PrepareLadderSymbolsV86.ps1." }
    return $text.Replace($old, $new)
}

$code = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")

# 1. DrawElement passa a delegar o desenho ao LadderSymbols.
$start = $code.IndexOf('        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background)')
$end = $code.IndexOf('        /// <summary>Converte um ponto da tela em celula.')
if ($start -lt 0 -or $end -lt $start) { throw 'V86: regiao de DrawElement nao encontrada.' }
$wrapper = @'
        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background, LadderPower power)
        {
            LadderSymbols.Draw(g, element, cell, y, branch, background, power);
        }

'@
$code = $code.Substring(0, $start) + $wrapper.Replace("`r`n", "`n") + $code.Substring($end)

# 2. Chamadas passam o estado de energizacao.
$code = Rep $code 'DrawElement(g, Rungs[r].Elements[c], mainCell, y, false, ElementBackground(r, c, false));' 'DrawElement(g, Rungs[r].Elements[c], mainCell, y, false, ElementBackground(r, c, false), PowerFor(r, c, false));' 'chamada principal'
$code = Rep $code 'DrawElement(g, branch, branchCell, branchY, true, ElementBackground(r, c, true));' 'DrawElement(g, branch, branchCell, branchY, true, ElementBackground(r, c, true), PowerFor(r, c, true));' 'chamada do ramo'

# 3. Fio da linha energizada.
$code = Rep $code '                using (Pen wirePen = new Pen(OpenLadderPalette.Wire, 2.0f))
                    g.DrawLine(wirePen, LeftRail, y, rightRail, y);' '                bool rungOn = rungPower != null && r < rungPower.Length && rungPower[r];
                using (Pen wirePen = new Pen(rungOn ? OpenLadderPalette.Ok : OpenLadderPalette.Wire, rungOn ? 2.8f : 2.0f))
                    g.DrawLine(wirePen, LeftRail, y, rightRail, y);' 'fio da linha'

# 4. Estado de energizacao: campo, API publica e regra por elemento.
$state = @'
        private const int RightMargin = 42;

        private bool[] rungPower;

        /// <summary>
        /// Resultado da ultima varredura por linha (true = energizada). Null limpa o
        /// destaque. Sem dados por elemento, so se afirma conducao onde a coluna nao
        /// tem ramo paralelo; nas demais o simbolo fica neutro.
        /// </summary>
        public void SetRungPower(bool[] power)
        {
            rungPower = power;
            Invalidate();
        }

        private LadderPower PowerFor(int rung, int column, bool branch)
        {
            if (rungPower == null || rung < 0 || rung >= rungPower.Length) return LadderPower.Unknown;
            if (Rungs[rung].Parallel[column].Type != LadderElementType.Empty && column < LadderRung.ColumnCount - 1) return LadderPower.Unknown;
            return rungPower[rung] ? LadderPower.On : LadderPower.Off;
        }
'@
$code = Rep $code '        private const int RightMargin = 42;' $state.TrimEnd() 'estado de energizacao'

# 5. Cabecalho C1..C8 e rotulos em pixels (nao dependem da escala de DPI).
$code = Rep $code 'string label = (c + 1).ToString("00");' 'string label = "C" + (c + 1).ToString();' 'rotulo de coluna'
$code = Rep $code 'new Font("Segoe UI Semibold", 7.0f, FontStyle.Bold)' 'new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Pixel)' 'fonte do cabecalho'
$code = Rep $code 'left + (cellWidth - size.Width) / 2f, 3f);' 'left + (cellWidth - size.Width) / 2f, 4.5f);' 'posicao do rotulo de coluna'
$code = Rep $code 'new Font("Segoe UI Semibold", 7.8f, FontStyle.Bold)' 'new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel)' 'fonte da calha'
$code = Rep $code 'badge.Left + (badge.Width - size.Width) / 2f, badge.Top + 5f);' 'badge.Left + (badge.Width - size.Width) / 2f, badge.Top + 5.5f);' 'posicao do rotulo de linha'

[IO.File]::WriteAllText($path, $code, (New-Object System.Text.UTF8Encoding($true)))
Write-Host 'V86: simbolos IEC 61131-3 e cabecalho C1..C8 aplicados.'

# 6. A simulacao colore o diagrama do editor: o shell repassa o resultado de cada
# varredura ao canvas (so quando o simulador roda o projeto do editor).
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V86: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")
$shell = Rep $shell 'if (simulatorForm == null || simulatorForm.IsDisposed) simulatorForm = new LadderSimulatorForm();' 'if (simulatorForm == null || simulatorForm.IsDisposed)
            {
                simulatorForm = new LadderSimulatorForm();
                simulatorForm.PowerChanged += OnSimulatorPower;
            }' 'criacao do simulador'
$handler = @'
        private void OnSimulatorPower(bool[] power)
        {
            if (ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic);
                LadderCanvas canvas = field == null ? null : field.GetValue(ladderForm) as LadderCanvas;
                if (canvas != null) canvas.SetRungPower(power);
            }
            catch { }
        }

        private void ShowLadder()
'@
$shell = Rep $shell '        private void ShowLadder()' $handler.TrimEnd() 'insercao do repasse de energizacao'
[IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($true)))
Write-Host 'V86: repasse de energizacao simulador -> editor aplicado.'

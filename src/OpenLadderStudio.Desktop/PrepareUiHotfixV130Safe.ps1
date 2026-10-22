$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V130: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "V130: ancora nao encontrada ($label)."
}

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V130: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V130: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# Hotfix deliberadamente minimo:
# - nao toca no LadderEditor.build.cs;
# - nao recria paleta/aba interna;
# - nao renomeia nenhuma instrucao;
# - apenas amplia a paleta INSTRUCOES que ja existe no shell principal.
$shell = Replace-Required $shell '            p.Width = 292;' '            p.Width = 352;' 'largura do painel Instrucoes'
$shell = Replace-Required $shell '            search.Size = new Size(262, 24);' '            search.Size = new Size(322, 26);' 'largura da busca de Instrucoes'

$sectionMethod = @'
        private static void V73AddSection(FlowLayoutPanel list, string text)
        {
            Label section = new Label();
            section.Width = 320;
            section.Height = 27;
            section.Margin = new Padding(8, 10, 0, 2);
            section.Text = text;
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.ForeColor = StudioTheme.Faint;
            section.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            list.Controls.Add(section);
        }

'@
$shell = Replace-Section $shell '        private static void V73AddSection(FlowLayoutPanel list, string text)' '        private void V73AddInstruction' $sectionMethod 'secoes da paleta Instrucoes'

$instructionMethod = @'
        private void V73AddInstruction(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 320;
            b.Height = 44;
            b.Margin = new Padding(0, 0, 0, 2);
            b.Click += delegate { V73SelectLadderTool(tool); };
            list.Controls.Add(b);
        }

'@
$shell = Replace-Section $shell '        private void V73AddInstruction(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)' '        private void V73AddAction' $instructionMethod 'itens da paleta Instrucoes'

$actionMethod = @'
        private void V73AddAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 320;
            b.Height = 44;
            b.Margin = new Padding(0, 0, 0, 2);
            if (action != null) b.Click += action;
            list.Controls.Add(b);
        }

'@
$shell = Replace-Section $shell '        private void V73AddAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)' '        private void V73SelectLadderTool' $actionMethod 'acoes da paleta Instrucoes'

# Guardrails sem texto acentuado para nao depender da etapa de normalizacao PT-BR.
if (-not $shell.Contains('V73InstructionPanel()')) { throw 'V130: painel principal de Instrucoes ausente.' }
if (-not $shell.Contains('V73AddInstruction(list, "Contato NA"')) { throw 'V130: itens Ladder esperados ausentes.' }
if (-not $shell.Contains('V73AddInstruction(list, "Bobina"')) { throw 'V130: Bobina ausente.' }
if (-not $shell.Contains('V73AddInstruction(list, "END"')) { throw 'V130: END ausente.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'UI V130 SAFE aplicada: paleta principal ampliada, sem recriar painel/aba e sem renomear instrucoes.' -ForegroundColor Cyan

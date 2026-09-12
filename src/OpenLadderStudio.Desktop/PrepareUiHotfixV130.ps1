$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V130: UniversalStudioShell.build.cs nao encontrado.' }

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

$shell = [System.IO.File]::ReadAllText($shellPath)

# -----------------------------------------------------------------------------
# 1. Barra TP02: mantem os nomes originais da v1.28 e apenas melhora o aspecto.
# Nenhum simbolo Unicode e inserido no texto dos botoes para evitar nomes/glyphs
# inconsistentes em diferentes fontes e versoes do Windows.
# -----------------------------------------------------------------------------
$bar = @'
        private Control BuildTp02HomeBarV126()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 62;
            bar.Fill = Color.FromArgb(31, 34, 38);
            bar.BottomLine = Border;

            Label title = new Label();
            title.Text = "TP02 / TP-232PG";
            title.AutoSize = false;
            title.Location = new Point(14, 9);
            title.Size = new Size(126, 42);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = Fore;
            title.Font = new Font("Segoe UI Semibold", 9.1f, FontStyle.Bold);
            bar.Controls.Add(title);

            tp02ConnectButtonV128 = NewTp02HomeButtonV126("CONECTAR", 145, 122, true,
                delegate { ToggleTp02HomeConnectionV128(); });
            bar.Controls.Add(tp02ConnectButtonV128);

            tp02ConnectionLabelV128 = new Label();
            tp02ConnectionLabelV128.Text = "DESCONECTADO";
            tp02ConnectionLabelV128.AutoSize = false;
            tp02ConnectionLabelV128.Location = new Point(275, 12);
            tp02ConnectionLabelV128.Size = new Size(198, 36);
            tp02ConnectionLabelV128.TextAlign = ContentAlignment.MiddleCenter;
            tp02ConnectionLabelV128.ForeColor = Muted;
            tp02ConnectionLabelV128.BackColor = Color.FromArgb(24, 27, 31);
            tp02ConnectionLabelV128.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            bar.Controls.Add(tp02ConnectionLabelV128);

            int x = 483;
            tp02ReadButtonV128 = NewTp02HomeButtonV126("LER", x, 90, false,
                delegate { ExecuteTp02HomeCommandV126("READ"); });
            bar.Controls.Add(tp02ReadButtonV128);
            x += 98;

            tp02WriteButtonV128 = NewTp02HomeButtonV126("ESCREVER", x, 112, true,
                delegate { ExecuteTp02HomeCommandV126("WRITE"); });
            bar.Controls.Add(tp02WriteButtonV128);
            x += 120;

            tp02VerifyButtonV128 = NewTp02HomeButtonV126("VERIFICAR", x, 116, false,
                delegate { ExecuteTp02HomeCommandV126("VERIFY"); });
            bar.Controls.Add(tp02VerifyButtonV128);
            x += 124;

            tp02StopButtonV128 = NewTp02HomeButtonV126("STOP", x, 90, false,
                delegate { ExecuteTp02HomeCommandV126("STOP"); });
            tp02StopButtonV128.ForeColor = Color.White;
            bar.Controls.Add(tp02StopButtonV128);
            x += 98;

            tp02RunButtonV128 = NewTp02HomeButtonV126("RUN", x, 90, false,
                delegate { ExecuteTp02HomeCommandV126("RUN"); });
            tp02RunButtonV128.ForeColor = Color.White;
            bar.Controls.Add(tp02RunButtonV128);

            UpdateTp02HomeButtonsV128();
            return bar;
        }

'@
$shell = Replace-Section $shell '        private Control BuildTp02HomeBarV126()' '        private void ToggleTp02HomeConnectionV128()' $bar 'barra TP02 v1.30'

$button = @'
        private Button NewTp02HomeButtonV126(string text, int left, int width, bool primary, EventHandler action)
        {
            string key = (text ?? string.Empty).Trim().ToUpperInvariant();
            Color normal = Color.FromArgb(57, 64, 73);
            Color hover = Color.FromArgb(70, 78, 89);
            Color pressed = Color.FromArgb(45, 51, 59);
            Color border = Color.FromArgb(86, 95, 108);

            if (key == "CONECTAR" || key == "DESCONECTAR")
            {
                normal = Color.FromArgb(42, 101, 186);
                hover = Color.FromArgb(53, 119, 214);
                pressed = Color.FromArgb(32, 80, 151);
                border = Color.FromArgb(80, 142, 224);
            }
            else if (key == "LER")
            {
                normal = Color.FromArgb(45, 104, 160);
                hover = Color.FromArgb(55, 123, 188);
                pressed = Color.FromArgb(34, 82, 128);
                border = Color.FromArgb(76, 137, 194);
            }
            else if (key == "ESCREVER" || key == "RUN")
            {
                normal = Color.FromArgb(37, 137, 91);
                hover = Color.FromArgb(44, 158, 105);
                pressed = Color.FromArgb(29, 108, 72);
                border = Color.FromArgb(64, 168, 119);
            }
            else if (key == "VERIFICAR")
            {
                normal = Color.FromArgb(162, 108, 32);
                hover = Color.FromArgb(187, 127, 40);
                pressed = Color.FromArgb(128, 84, 24);
                border = Color.FromArgb(203, 146, 63);
            }
            else if (key == "STOP")
            {
                normal = Color.FromArgb(166, 62, 62);
                hover = Color.FromArgb(194, 72, 72);
                pressed = Color.FromArgb(133, 48, 48);
                border = Color.FromArgb(211, 91, 91);
            }

            Button button = new Button();
            button.Text = key;
            button.Location = new Point(left, 10);
            button.Size = new Size(width, 42);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            button.BackColor = normal;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = pressed;
            if (action != null) button.Click += action;
            return button;
        }

'@
$shell = Replace-Section $shell '        private Button NewTp02HomeButtonV126' '        private void ExecuteTp02HomeCommandV126' $button 'botoes TP02 v1.30'

# O estado de conexao altera apenas o texto, sem simbolos adicionais.
$shell = Replace-Required $shell 'tp02ConnectButtonV128.Text = connected ? "DESCONECTAR" : "CONECTAR";' 'tp02ConnectButtonV128.Text = connected ? "DESCONECTAR" : "CONECTAR";' 'texto conectar/desconectar'

# -----------------------------------------------------------------------------
# 2. Aumenta a paleta correta: o painel INSTRUCOES do shell principal.
# A paleta interna do LadderEditor nao e recriada nem renomeada nesta versao.
# -----------------------------------------------------------------------------
$shell = Replace-Required $shell '            p.Width = 292;' '            p.Width = 352;' 'largura painel Instrucoes'
$shell = Replace-Required $shell '            search.Size = new Size(262, 24);' '            search.Size = new Size(322, 26);' 'largura busca Instrucoes'

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
$shell = Replace-Section $shell '        private static void V73AddSection(FlowLayoutPanel list, string text)' '        private void V73AddInstruction' $sectionMethod 'secoes da paleta correta'

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
            b.Font = new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold);
            b.Click += delegate { V73SelectLadderTool(tool); };
            list.Controls.Add(b);
        }

'@
$shell = Replace-Section $shell '        private void V73AddInstruction(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)' '        private void V73AddAction' $instructionMethod 'instrucoes maiores'

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
            b.Font = new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold);
            if (action != null) b.Click += action;
            list.Controls.Add(b);
        }

'@
$shell = Replace-Section $shell '        private void V73AddAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)' '        private void V73SelectLadderTool' $actionMethod 'acoes maiores'

# Guardrails: nomes existentes devem permanecer na paleta principal.
$requiredNames = @('Contato NA', 'Contato NF', 'Ramo paralelo NA', 'Ramo paralelo NF', 'Bobina', 'SET', 'RESET', 'Temporizador', 'Contador', 'Borda de subida', 'Borda de descida', 'Funcao especial', 'END', 'Selecionar')
foreach ($name in $requiredNames) {
    if ($name -eq 'Funcao especial') {
        if (-not ($shell.Contains('Função especial') -or $shell.Contains('Funcao especial'))) { throw 'V130: nome Função especial ausente.' }
    }
    elseif (-not $shell.Contains($name)) { throw "V130: nome esperado ausente: $name" }
}

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'UI V130 aplicada: uma unica paleta lateral, nomes preservados e elementos ampliados.' -ForegroundColor Cyan

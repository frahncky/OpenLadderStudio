$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V129: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $ladderPath)) { throw 'V129: LadderEditor.build.cs nao encontrado.' }

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V129: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V129: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "V129: ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}

# -----------------------------------------------------------------------------
# Barra TP02: botoes maiores, cores funcionais, simbolos, hover/pressed e pill
# de estado. Nenhuma regra de comunicacao e alterada nesta etapa.
# -----------------------------------------------------------------------------
$shell = [System.IO.File]::ReadAllText($shellPath)

$buildBar = @'
        private Control BuildTp02HomeBarV126()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 66;
            bar.Fill = Color.FromArgb(27, 30, 34);
            bar.BottomLine = Border;

            Label title = new Label();
            title.Text = "TP02 / TP-232PG";
            title.AutoSize = false;
            title.Location = new Point(14, 10);
            title.Size = new Size(126, 44);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = Fore;
            title.Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold);
            bar.Controls.Add(title);

            tp02ConnectButtonV128 = NewTp02HomeButtonV126("CONECTAR", 145, 128, true,
                delegate { ToggleTp02HomeConnectionV128(); });
            bar.Controls.Add(tp02ConnectButtonV128);

            tp02ConnectionLabelV128 = new Label();
            tp02ConnectionLabelV128.Text = "DESCONECTADO";
            tp02ConnectionLabelV128.AutoSize = false;
            tp02ConnectionLabelV128.Location = new Point(282, 13);
            tp02ConnectionLabelV128.Size = new Size(214, 38);
            tp02ConnectionLabelV128.TextAlign = ContentAlignment.MiddleCenter;
            tp02ConnectionLabelV128.ForeColor = Muted;
            tp02ConnectionLabelV128.BackColor = Color.FromArgb(20, 23, 27);
            tp02ConnectionLabelV128.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            ApplyRoundedRegionV129(tp02ConnectionLabelV128, 9);
            bar.Controls.Add(tp02ConnectionLabelV128);

            int x = 506;
            tp02ReadButtonV128 = NewTp02HomeButtonV126("LER", x, 92, false,
                delegate { ExecuteTp02HomeCommandV126("READ"); });
            bar.Controls.Add(tp02ReadButtonV128);
            x += 100;

            tp02WriteButtonV128 = NewTp02HomeButtonV126("ESCREVER", x, 118, true,
                delegate { ExecuteTp02HomeCommandV126("WRITE"); });
            bar.Controls.Add(tp02WriteButtonV128);
            x += 126;

            tp02VerifyButtonV128 = NewTp02HomeButtonV126("VERIFICAR", x, 124, false,
                delegate { ExecuteTp02HomeCommandV126("VERIFY"); });
            bar.Controls.Add(tp02VerifyButtonV128);
            x += 132;

            tp02StopButtonV128 = NewTp02HomeButtonV126("STOP", x, 96, false,
                delegate { ExecuteTp02HomeCommandV126("STOP"); });
            bar.Controls.Add(tp02StopButtonV128);
            x += 104;

            tp02RunButtonV128 = NewTp02HomeButtonV126("RUN", x, 96, false,
                delegate { ExecuteTp02HomeCommandV126("RUN"); });
            bar.Controls.Add(tp02RunButtonV128);

            UpdateTp02HomeButtonsV128();
            return bar;
        }

'@
$shell = Replace-Section $shell '        private Control BuildTp02HomeBarV126()' '        private void ToggleTp02HomeConnectionV128()' $buildBar 'barra TP02 personalizada'

$buttonMethods = @'
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
            else if (key == "ESCREVER")
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
            else if (key == "RUN")
            {
                normal = Color.FromArgb(34, 139, 78);
                hover = Color.FromArgb(41, 163, 92);
                pressed = Color.FromArgb(26, 109, 61);
                border = Color.FromArgb(61, 181, 108);
            }

            Button button = new Button();
            button.Text = DecorateTp02ButtonCaptionV129(key);
            button.Location = new Point(left, 10);
            button.Size = new Size(width, 44);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = new Font("Segoe UI Semibold", 9.1f, FontStyle.Bold);
            button.BackColor = normal;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = pressed;
            ApplyRoundedRegionV129(button, 9);
            button.Resize += delegate { ApplyRoundedRegionV129(button, 9); };

            ToolTip tip = new ToolTip();
            tip.InitialDelay = 350;
            tip.ReshowDelay = 100;
            tip.AutoPopDelay = 5000;
            tip.ShowAlways = true;
            tip.SetToolTip(button, Tp02ButtonToolTipV129(key));
            button.Tag = tip;

            if (action != null) button.Click += action;
            return button;
        }

        private static string DecorateTp02ButtonCaptionV129(string key)
        {
            string value = (key ?? string.Empty).Trim().ToUpperInvariant();
            if (value == "CONECTAR") return "●  CONECTAR";
            if (value == "DESCONECTAR") return "×  DESCONECTAR";
            if (value == "LER") return "↓  LER";
            if (value == "ESCREVER") return "↑  ESCREVER";
            if (value == "VERIFICAR") return "✓  VERIFICAR";
            if (value == "STOP") return "■  STOP";
            if (value == "RUN") return "▶  RUN";
            return value;
        }

        private static string Tp02ButtonToolTipV129(string key)
        {
            string value = (key ?? string.Empty).Trim().ToUpperInvariant();
            if (value == "CONECTAR") return "Estabelecer comunicacao com o TP02 pelo TP-232PG.";
            if (value == "DESCONECTAR") return "Encerrar a referencia de conexao atual.";
            if (value == "LER") return "Ler o programa armazenado no TP02.";
            if (value == "ESCREVER") return "Gravar o projeto Ladder atual no TP02. Requer STOP.";
            if (value == "VERIFICAR") return "Reler e comparar o programa sem gravar novamente.";
            if (value == "STOP") return "Solicitar STOP quando o quadro PG remoto estiver validado.";
            if (value == "RUN") return "Solicitar RUN quando o quadro PG remoto estiver validado.";
            return value;
        }

        private static void ApplyRoundedRegionV129(Control control, int radius)
        {
            if (control == null || control.Width < 2 || control.Height < 2) return;
            int d = Math.Max(2, radius * 2);
            Rectangle r = new Rectangle(0, 0, control.Width - 1, control.Height - 1);
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(r.Left, r.Top, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                control.Region = new Region(path);
            }
        }

'@
$shell = Replace-Section $shell '        private Button NewTp02HomeButtonV126' '        private void ExecuteTp02HomeCommandV126' $buttonMethods 'botoes TP02 v1.29'

$shell = Replace-Required $shell 'tp02ConnectButtonV128.Text = connected ? "DESCONECTAR" : "CONECTAR";' 'tp02ConnectButtonV128.Text = DecorateTp02ButtonCaptionV129(connected ? "DESCONECTAR" : "CONECTAR");' 'icone conectar/desconectar'
$shell = $shell.Replace('tp02ConnectionLabelV128.ForeColor = Muted;', 'tp02ConnectionLabelV128.ForeColor = Muted;`r`n                    tp02ConnectionLabelV128.BackColor = Color.FromArgb(20, 23, 27);')
$shell = $shell.Replace('tp02ConnectionLabelV128.ForeColor = Accent;', 'tp02ConnectionLabelV128.ForeColor = Color.FromArgb(112, 224, 158);`r`n                    tp02ConnectionLabelV128.BackColor = Color.FromArgb(24, 54, 40);')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Paleta Ladder: amplia largura, altura, simbolos e fonte, e organiza os itens
# por grupos. O comportamento das ferramentas nao muda.
# -----------------------------------------------------------------------------
$ladder = [System.IO.File]::ReadAllText($ladderPath)

$toolboxBlock = @'
            Panel toolbox = new Panel();
            toolbox.Dock = DockStyle.Left;
            toolbox.Width = 326;
            toolbox.BackColor = SideBg;
            toolbox.AutoScroll = true;
            toolbox.Padding = new Padding(0, 0, 0, 18);

            Label toolsTitle = new Label();
            toolsTitle.Text = "ELEMENTOS LADDER • TP02";
            toolsTitle.AutoSize = true;
            toolsTitle.Font = new Font("Segoe UI Semibold", 11.0f, FontStyle.Bold);
            toolsTitle.ForeColor = OpenLadderPalette.Fore;
            toolsTitle.Location = new Point(16, 16);
            toolbox.Controls.Add(toolsTitle);

            int t = 52;
            AddToolSectionV129(toolbox, "EDICAO", ref t);
            AddToolButton(toolbox, "↖   SELECIONAR", t, LadderTool.Select); t += 52;
            AddToolButton(toolbox, "×   APAGAR ELEMENTO", t, LadderTool.Erase); t += 58;

            AddToolSectionV129(toolbox, "CONTATOS", ref t);
            AddToolButton(toolbox, "—| |—   CONTATO NA", t, LadderTool.ContactNO); t += 52;
            AddToolButton(toolbox, "—|/|—   CONTATO NF", t, LadderTool.ContactNC); t += 52;
            AddToolButton(toolbox, "↳ | |    PARALELO NA", t, LadderTool.ParallelNO); t += 52;
            AddToolButton(toolbox, "↳ |/|    PARALELO NF", t, LadderTool.ParallelNC); t += 58;

            AddToolSectionV129(toolbox, "SAIDAS", ref t);
            AddToolButton(toolbox, "—( )—   OUT / BOBINA", t, LadderTool.Coil); t += 52;
            AddToolButton(toolbox, "F-23     SET", t, LadderTool.Set); t += 52;
            AddToolButton(toolbox, "F-24     RESET", t, LadderTool.Reset); t += 58;

            AddToolSectionV129(toolbox, "TEMPO E CONTAGEM", ref t);
            AddToolButton(toolbox, "TMR      TEMPORIZADOR", t, LadderTool.Timer); t += 52;
            AddToolButton(toolbox, "CNT      CONTADOR", t, LadderTool.Counter); t += 58;

            AddToolSectionV129(toolbox, "EVENTOS", ref t);
            AddToolButton(toolbox, "F-05  ↑  BORDA DE SUBIDA", t, LadderTool.EdgeUp); t += 52;
            AddToolButton(toolbox, "F-06  ↓  BORDA DE DESCIDA", t, LadderTool.EdgeDown); t += 58;

            AddToolSectionV129(toolbox, "FUNCOES", ref t);
            AddToolButton(toolbox, "FUN      FUNCAO ESPECIAL", t, LadderTool.Function); t += 52;
            AddToolButton(toolbox, "F-00     END", t, LadderTool.End); t += 62;

            Label help = new Label();
            help.Text = "TP02: X/Y/C/SC para logica\r\nTMR/CNT: V0001 a V0256\r\nDuplo clique: editar parametro\r\nCtrl+Z: desfazer • Del: apagar";
            help.AutoSize = true;
            help.MaximumSize = new Size(286, 0);
            help.Font = new Font("Segoe UI", 9.0f);
            help.ForeColor = TextSecondary;
            help.Location = new Point(16, t);
            toolbox.Controls.Add(help);

'@
$ladder = Replace-Section $ladder '            Panel toolbox = new Panel();' '            Panel editorHost = new Panel();' $toolboxBlock 'paleta Ladder ampliada'

$toolMethods = @'
        private void AddToolSectionV129(Control parent, string text, ref int top)
        {
            Label section = new Label();
            section.Text = text;
            section.AutoSize = false;
            section.Location = new Point(14, top);
            section.Size = new Size(286, 24);
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.ForeColor = TextSecondary;
            section.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            parent.Controls.Add(section);
            top += 27;
        }

        private void AddToolButton(Control parent, string text, int top, LadderTool tool)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Tag = tool;
            b.Location = new Point(10, top);
            b.Size = new Size(296, 46);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(14, 0, 8, 0);
            b.NormalColor = Color.FromArgb(35, 39, 45);
            b.HoverColor = Color.FromArgb(47, 78, 68);
            b.ForeColor = OpenLadderPalette.Fore;
            b.Font = new Font("Segoe UI Semibold", 10.0f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.Click += delegate { SetActiveTool(tool); };

            ToolTip tip = new ToolTip();
            tip.InitialDelay = 300;
            tip.AutoPopDelay = 4500;
            tip.SetToolTip(b, "Selecionar: " + ToolName(tool));
            parent.Controls.Add(b);
        }

'@
$ladder = Replace-Section $ladder '        private void AddToolButton(Control parent, string text, int top, LadderTool tool)' '        private void SetActiveTool(LadderTool tool)' $toolMethods 'botoes grandes da paleta Ladder'

[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)
Write-Host 'UI V129 aplicada: botoes TP02 personalizados e paleta Ladder ampliada/agrupada.'

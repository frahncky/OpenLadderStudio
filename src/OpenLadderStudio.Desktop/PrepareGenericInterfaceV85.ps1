$ErrorActionPreference = 'Stop'
# V85 - Interface generica e agrupada por tarefa.
# Roda por ultimo na cadeia (depois dos Prepare do TP02), porque reorganiza
# menu e barra de ferramentas que aqueles scripts ainda alimentam. Menus,
# barra e rotulos usam termos neutros (Conectar, Ler do PLC, Transferir para
# o PLC, Monitorar); o que e especifico de um modelo fica em "Recursos do
# modelo", visivel somente quando o driver do controlador ativo o oferece.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V85: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $uiPath)) { throw 'V85: StudioUi.build.cs nao encontrado.' }

function Normalize-Nl([string]$text, [string]$nl) { return ($text -replace "`r?`n", $nl) }
function Get-Nl([string]$text) { if ($text.Contains("`r`n")) { return "`r`n" } else { return "`n" } }

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    $nl = Get-Nl $text
    $old = Normalize-Nl $old $nl
    $new = Normalize-Nl $new $nl
    if (-not $text.Contains($old)) { throw "V85: ancora nao encontrada ($label). Ajuste PrepareGenericInterfaceV85.ps1." }
    return $text.Replace($old, $new)
}

function Rep-Region([string]$text, [string]$start, [string]$end, [string]$new, [string]$label) {
    $s = $text.IndexOf($start)
    if ($s -lt 0) { throw "V85: inicio da regiao nao encontrado ($label)." }
    $e = $text.IndexOf($end, $s + $start.Length)
    if ($e -lt 0) { throw "V85: fim da regiao nao encontrado ($label)." }
    return $text.Substring(0, $s) + (Normalize-Nl $new (Get-Nl $text)) + $text.Substring($e)
}

$shell = [IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")
$ui = [IO.File]::ReadAllText($uiPath).Replace("`r`n", "`n")

# ---------------------------------------------------------------- icones
# "Ler do PLC" (seta para baixo) e "Transferir para o PLC" (seta para cima)
# precisam ser distinguiveis; os dois usavam o mesmo desenho.
$ui = Rep $ui 'Coil, CoilSet, CoilReset, Timer, Counter, Trash' 'Coil, CoilSet, CoilReset, Timer, Counter, Trash, Upload' 'enum de icones'
$uploadGlyph = @'
                    case StudioIcon.Upload:
                        g.DrawLine(p, cx, y + h * 0.62f, cx, y + h * 0.14f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.30f, y + h * 0.34f), new PointF(cx, y + h * 0.12f),
                            new PointF(x + w * 0.70f, y + h * 0.34f) });
                        g.DrawLine(p, x + w * 0.18f, y + h * 0.86f, x + w * 0.82f, y + h * 0.86f);
                        break;

                    case StudioIcon.Chip:
'@
$ui = Rep $ui '                    case StudioIcon.Chip:' $uploadGlyph.TrimEnd() 'desenho do icone Upload'
$ui = Rep $ui '                case StudioIcon.Download: return OpenLadderPalette.Duo(88, 166, 230, 24, 116, 190);' ('                case StudioIcon.Download: return OpenLadderPalette.Duo(88, 166, 230, 24, 116, 190);' + "`r`n" + '                case StudioIcon.Upload:   return OpenLadderPalette.Duo(88, 166, 230, 24, 116, 190);') 'cor do icone Upload'

# ---------------------------------------------------------------- menu
$menu = @'
        private ToolStripMenuItem DropItemKeys(string text, string keys, string tip, EventHandler click)
        {
            ToolStripMenuItem m = DropItem(text, click);
            if (!string.IsNullOrEmpty(keys)) m.ShortcutKeyDisplayString = keys;
            if (!string.IsNullOrEmpty(tip)) m.ToolTipText = tip;
            return m;
        }

        /// <summary>
        /// Ponto unico de entrada para enviar o programa ao PLC. Cada driver com
        /// tela propria de transferencia entra aqui; os demais caem na central
        /// generica, que explica o que o driver declara suportar.
        /// </summary>
        private void ShowProgramTransfer()
        {
            RefreshProfileUi();
            if (IsTp02()) { ShowTp02ProgramTransfer(); return; }
            V73ShowTransferCenter();
        }

        private void ShowKeyboardShortcuts()
        {
            MessageBox.Show(this,
                "Arquivo\r\n"
                + "  Ctrl+N    Novo projeto\r\n"
                + "  Ctrl+O    Abrir\r\n"
                + "  Ctrl+S    Salvar\r\n"
                + "  Ctrl+Shift+S    Salvar como\r\n\r\n"
                + "Edição\r\n"
                + "  Ctrl+Z    Desfazer\r\n"
                + "  Ctrl+Y    Refazer\r\n"
                + "  Del    Apagar o elemento selecionado\r\n"
                + "  Esc    Voltar à ferramenta de seleção\r\n\r\n"
                + "Exibição\r\n"
                + "  Ctrl++    Aumentar zoom\r\n"
                + "  Ctrl+-    Diminuir zoom\r\n"
                + "  Ctrl+0    Zoom 100%",
                "Atalhos de teclado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;
            menu.Height = 27;
            menu.BackColor = Chrome;
            menu.ForeColor = Fore;
            menu.Padding = new Padding(8, 2, 0, 2);
            menu.ShowItemToolTips = true;
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new UniversalStudioColorTable());

            ToolStripMenuItem arquivo = MenuItem("Arquivo");
            arquivo.DropDownItems.Add(DropItemKeys("Novo projeto", "Ctrl+N", "Cria um projeto Ladder vazio", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(DropItemKeys("Abrir...", "Ctrl+O", "Abre um projeto salvo", delegate { InvokeLadder("OpenProject", null); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(DropItemKeys("Salvar", "Ctrl+S", "Salva o projeto atual", delegate { InvokeLadder("SaveProject", new object[] { false }); }));
            arquivo.DropDownItems.Add(DropItemKeys("Salvar como...", "Ctrl+Shift+S", "Salva uma cópia com outro nome", delegate { InvokeLadder("SaveProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(DropItemKeys("Sair", "Alt+F4", null, delegate { Close(); }));

            ToolStripMenuItem editar = MenuItem("Editar");
            editar.DropDownItems.Add(DropItemKeys("Desfazer", "Ctrl+Z", "Desfaz a última alteração do diagrama", delegate { InvokeLadder("Undo", null); }));
            editar.DropDownItems.Add(DropItemKeys("Refazer", "Ctrl+Y", "Refaz a alteração desfeita", delegate { InvokeLadder("Redo", null); }));
            editar.DropDownItems.Add(new ToolStripSeparator());
            editar.DropDownItems.Add(DropItemKeys("Apagar elemento selecionado", "Del", "Remove só o elemento selecionado; a linha continua", delegate { InvokeLadder("DeleteSelectedElement", null); }));
            editar.DropDownItems.Add(DropItemKeys("Adicionar linha", null, "Acrescenta uma linha ao final do diagrama", delegate { InvokeLadder("AddRung", null); }));
            editar.DropDownItems.Add(DropItemKeys("Excluir linha selecionada", null, "Remove a linha inteira, com todos os elementos", delegate { InvokeLadder("DeleteSelectedRung", null); }));

            miNav = DropItem("Painel de projeto", delegate { TogglePanel(0); });
            miProps = DropItem("Painel de instruções", delegate { TogglePanel(1); });
            miConsole = DropItem("Painel de mensagens", delegate { TogglePanel(2); });
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;

            ToolStripMenuItem exibir = MenuItem("Exibir");
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);
            exibir.DropDownItems.Add(DropItem("Restaurar layout padrão", delegate { V75ResetWorkspaceLayout(); }));
            exibir.DropDownItems.Add(new ToolStripSeparator());
            exibir.DropDownItems.Add(DropItemKeys("Aumentar zoom", "Ctrl++", null, delegate { V74Zoom("ZoomIn"); }));
            exibir.DropDownItems.Add(DropItemKeys("Diminuir zoom", "Ctrl+-", null, delegate { V74Zoom("ZoomOut"); }));
            exibir.DropDownItems.Add(DropItemKeys("Zoom 100%", "Ctrl+0", "Ajusta o diagrama à largura disponível", delegate { V74Zoom("ZoomReset"); }));
            exibir.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem tema = MenuItem("Tema");
            tema.DropDownItems.Add(DropItem("Escuro", delegate { ApplyThemeChoice(OpenLadderThemeMode.Dark); }));
            tema.DropDownItems.Add(DropItem("Claro", delegate { ApplyThemeChoice(OpenLadderThemeMode.Light); }));
            exibir.DropDownItems.Add(tema);

            ToolStripMenuItem projeto = MenuItem("Projeto");
            projeto.DropDownItems.Add(DropItem("Programa principal (Main)", delegate { ShowLadder(); }));
            projeto.DropDownItems.Add(new ToolStripSeparator());
            projeto.DropDownItems.Add(DropItemKeys("Compilar / validar", null, "Valida o programa e aponta problemas antes da transferência", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            projeto.DropDownItems.Add(DropItemKeys("Simular processo", null, "Executa o programa no PLC virtual com uma planta simulada", delegate { ShowSimulator(); }));
            projeto.DropDownItems.Add(new ToolStripSeparator());
            projeto.DropDownItems.Add(DropItemKeys("Modelo do PLC...", null, "Escolhe o fabricante, o modelo e o driver de comunicação", delegate { ShowDeviceManager(); }));

            ToolStripMenuItem plc = MenuItem("PLC");
            plc.DropDownItems.Add(DropItemKeys("Modelo do PLC...", null, "Escolhe o fabricante, o modelo e o driver de comunicação", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(new ToolStripSeparator());
            plc.DropDownItems.Add(DropItemKeys("Conectar...", null, "Abre a comunicação com o controlador selecionado", delegate { ShowCommunication(); }));
            plc.DropDownItems.Add(DropItemKeys("Ler do PLC", null, "Lê o programa gravado no controlador", delegate { ShowReader(); }));
            plc.DropDownItems.Add(DropItemKeys("Transferir para o PLC...", null, "Envia o programa do editor ao controlador", delegate { ShowProgramTransfer(); }));
            plc.DropDownItems.Add(DropItemKeys("Monitorar", null, "Acompanha entradas, saídas e registradores em tempo real", delegate { ShowMonitor(); }));
            plc.DropDownItems.Add(new ToolStripSeparator());
            plc.DropDownItems.Add(DropItemKeys("Mapa de memória...", null, "Cadastra e edita os endereços do controlador", delegate { ShowMemoryMapManager(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItemKeys("Verificar compatibilidade", null, "Confere se o Ladder do editor cabe no controlador selecionado", delegate { CheckPortability(); }));
            ferramentas.DropDownItems.Add(DropItemKeys("Diagnóstico", null, "Resumo do controlador e verificações que não escrevem no equipamento", delegate { V73ShowDiagnostics(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());

            // Tudo que pertence a um modelo fica aqui. O item inteiro some
            // quando o driver ativo nao o oferece (RefreshToolsForProfile).
            tp02Tools = MenuItem("Recursos do modelo");
            ToolStripMenuItem recursos = tp02Tools;
            recursos.DropDownItems.Add(DropItem("Validação física final...", delegate { ShowTp02PhysicalValidation(); }));
            recursos.DropDownItems.Add(DropItem("Validar escrita sem alterar o programa...", delegate { ShowTp02Pg33NoOpProbe(); }));
            recursos.DropDownItems.Add(new ToolStripSeparator());
            recursos.DropDownItems.Add(DropItem("Testar link de programação (PG)", delegate { LaunchInstalledTool("OpenLadderTP02PgLab.exe", "Laboratório PG TP02"); }));
            recursos.DropDownItems.Add(DropItem("Monitor MMI (somente leitura)", delegate { ShowTp02ReadOnlyMonitor(); }));
            recursos.DropDownItems.Add(DropItem("Capturar tráfego serial", delegate { LaunchInstalledTool("OpenLadderTP02Capture.exe", "Captura serial PC12/TP02"); }));
            recursos.DropDownItems.Add(new ToolStripSeparator());
            recursos.DropDownItems.Add(DropItem("Analisar projeto e tráfego serial", delegate { ShowTp02BridgeLab(); }));
            recursos.DropDownItems.Add(DropItem("Decodificar programa do controlador", delegate { ShowDecoder(); }));
            recursos.DropDownItems.Add(DropItem("Calibrar instruções", delegate { ShowCalibration(); }));
            recursos.DropDownItems.Add(DropItem("Converter IL para Ladder", delegate { ShowIl(); }));
            ferramentas.DropDownItems.Add(recursos);

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Atalhos de teclado", delegate { ShowKeyboardShortcuts(); }));
            ajuda.DropDownItems.Add(DropItem("Verificar atualizações", delegate { ShowUpdater(); }));
            ajuda.DropDownItems.Add(new ToolStripSeparator());
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this, "OpenLadder Studio\r\n\r\nEditor Ladder, simulação e comunicação com controladores compatíveis.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(projeto);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
            return menu;
        }
'@
$shell = Rep-Region $shell '        private MenuStrip BuildMenu()' '        private int toolCursor;' ($menu + "`r`n") 'BuildMenu'

# Botao "Conectar" nao precisa mais de largura propria: todos seguem o mesmo ritmo.
$shell = Rep $shell 'text == "Conectar" ? 132 : 92' '92' 'largura do botao Conectar'

# ---------------------------------------------------------------- barra
$toolbar = @'
        private void AddToolButtonTip(Control bar, string text, StudioIcon icon, string tip, EventHandler action)
        {
            AddToolButton(bar, text, icon, false, action);
            Control last = bar.Controls[bar.Controls.Count - 1];
            toolbarTips.SetToolTip(last, tip);
            last.AccessibleDescription = tip;
        }

        // Ordem: arquivo | historico | programa | PLC | zoom. So acoes frequentes;
        // o restante fica nos menus.
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 56;
            bar.Fill = Chrome;
            bar.BottomLine = OpenLadderPalette.HeaderLine;

            toolCursor = 8;
            AddToolButtonTip(bar, "Novo", StudioIcon.Doc, "Novo projeto (Ctrl+N)", delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButtonTip(bar, "Abrir", StudioIcon.Folder, "Abrir projeto (Ctrl+O)", delegate { InvokeLadder("OpenProject", null); });
            AddToolButtonTip(bar, "Salvar", StudioIcon.Save, "Salvar projeto (Ctrl+S)", delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButtonTip(bar, "Desfazer", StudioIcon.Undo, "Desfazer (Ctrl+Z)", delegate { InvokeLadder("Undo", null); });
            AddToolButtonTip(bar, "Refazer", StudioIcon.Redo, "Refazer (Ctrl+Y)", delegate { InvokeLadder("Redo", null); });
            AddToolSeparator(bar);
            AddToolButtonTip(bar, "Compilar", StudioIcon.Check, "Compilar e validar o programa", delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolButtonTip(bar, "Simulador", StudioIcon.Grid, "Simular o programa no PLC virtual", delegate { ShowSimulator(); });
            AddToolSeparator(bar);
            AddToolButtonTip(bar, "Conectar", StudioIcon.Plug, "Conectar ao controlador selecionado", delegate { ShowCommunication(); });
            AddToolButtonTip(bar, "Ler", StudioIcon.Download, "Ler o programa do PLC", delegate { ShowReader(); });
            AddToolButtonTip(bar, "Transferir", StudioIcon.Upload, "Transferir o programa para o PLC", delegate { ShowProgramTransfer(); });
            AddToolButtonTip(bar, "Monitorar", StudioIcon.Monitor, "Monitorar o PLC em tempo real", delegate { ShowMonitor(); });
            AddToolSeparator(bar);
            V74AddZoomControls(bar);
            return bar;
        }
'@
$shell = Rep-Region $shell '        private Control BuildToolbar()' '        private NavButton NavItem(' ($toolbar + "`r`n") 'BuildToolbar'

# ---------------------------------------------------------------- paleta
$shell = Rep $shell '            b.Click += delegate { V73SelectLadderTool(tool); };' ('            b.Click += delegate { V73SelectLadderTool(tool); };' + "`r`n" + '            toolbarTips.SetToolTip(b, text + " — escolha e clique no diagrama para inserir");') 'dica da instrucao'
$shell = Rep $shell "            if (action != null) b.Click += action;`r`n            list.Controls.Add(b);" "            if (action != null) b.Click += action;`r`n            toolbarTips.SetToolTip(b, text);`r`n            list.Controls.Add(b);" 'dica da acao'
$shell = Rep $shell '            searchHost.Controls.Add(search);' ('            searchHost.Controls.Add(search);' + "`r`n" + '            toolbarTips.SetToolTip(search, "Filtrar as instruções pelo nome");') 'dica da busca'
$filter = @'
            search.TextChanged += delegate
            {
                string q = (search.Text ?? string.Empty).Trim();
                foreach (Control c in list.Controls)
                {
                    NavButton b = c as NavButton;
                    if (b != null) b.Visible = q.Length == 0 || b.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
            };
'@
$filterNew = @'
            search.TextChanged += delegate
            {
                string q = (search.Text ?? string.Empty).Trim();
                // O titulo de uma secao so aparece se sobrar alguma instrucao nela.
                Label section = null;
                bool sectionHasMatch = false;
                foreach (Control c in list.Controls)
                {
                    Label heading = c as Label;
                    if (heading != null)
                    {
                        if (section != null) section.Visible = q.Length == 0 || sectionHasMatch;
                        section = heading;
                        sectionHasMatch = false;
                        continue;
                    }
                    NavButton b = c as NavButton;
                    if (b == null) continue;
                    bool match = q.Length == 0 || b.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
                    b.Visible = match;
                    if (match) sectionHasMatch = true;
                }
                if (section != null) section.Visible = q.Length == 0 || sectionHasMatch;
            };
'@
$shell = Rep $shell $filter $filterNew 'filtro da paleta'

# Na largura padrao o painel de instrucoes tem ~300 px, mas busca e itens eram
# fixos em 320: a caixa de busca saia pela borda direita. Acompanham o painel.
$fitPalette = @'
            Action fitPalette = delegate
            {
                int inner = Math.Max(180, list.ClientSize.Width - list.Padding.Horizontal - 2);
                foreach (Control c in list.Controls) c.Width = inner;
                search.Width = Math.Max(120, searchHost.ClientSize.Width - 28);
            };
            list.SizeChanged += delegate { fitPalette(); };
            searchHost.SizeChanged += delegate { fitPalette(); };

            host.Controls.Add(list);
            host.Controls.Add(searchHost);
'@
$shell = Rep $shell "            host.Controls.Add(list);`n            host.Controls.Add(searchHost);" $fitPalette.TrimEnd() 'ajuste de largura da paleta'

# ---------------------------------------------------------------- texto neutro
$toolsRefresh = @'
if (tp02Tools != null)
            {
                tp02Tools.Visible = IsTp02();
                tp02Tools.Text = currentProfile == null ? "Recursos do modelo" : "Recursos do modelo (" + currentProfile.Model + ")";
            }
'@
$shell = Rep $shell 'if (tp02Tools != null) tp02Tools.Visible = IsTp02();' $toolsRefresh.TrimEnd() 'visibilidade dos recursos do modelo'
$shell = Rep $shell '"TP02 ativo: os recursos avançados permanecem separados no menu Ferramentas e o monitor comum opera em leitura."' '"Os recursos específicos do modelo ativo ficam em Ferramentas > Recursos do modelo; o monitor comum opera em leitura."' 'texto do diagnostico (ativo)'
$shell = Rep $shell '"Os recursos avançados do TP02 só são usados quando um perfil TP02 está ativo."' '"Recursos específicos de um modelo só aparecem quando o controlador correspondente está selecionado."' 'texto do diagnostico (inativo)'
$shell = Rep $shell '"Leitura do programa - WEG TP02"' '"Leitura do programa do PLC"' 'titulo da aba de leitura'
$shell = Rep $shell '"Leitura RBP do TP02"' '"Leitura do programa do PLC"' 'status da leitura'
$shell = Rep $shell '"Monitor MMI - WEG TP02"' '"Monitor do PLC (somente leitura)"' 'titulo da aba do monitor'
$shell = Rep $shell '"Monitor TP02 em modo somente leitura"' '"Monitor do PLC em modo somente leitura"' 'status do monitor'
$shell = Rep $shell '"Conex\u00E3o - WEG TP02"' '"Conex\u00E3o com o PLC"' 'titulo do dialogo de conexao'
$shell = Rep $shell '"Escolha o tipo de comunica\u00E7\u00E3o com o TP02"' '"Escolha o tipo de comunica\u00E7\u00E3o com o PLC"' 'cabecalho do dialogo de conexao'
$shell = Rep $shell '"O TP02 possui caminhos diferentes. Programa\u00E7\u00E3o PG e MMI/Computer Link n\u00E3o s\u00E3o a mesma interface."' '"Este modelo oferece mais de um caminho de comunica\u00E7\u00E3o. Programa\u00E7\u00E3o (PG) e MMI/Computer Link n\u00E3o s\u00E3o a mesma interface."' 'texto do dialogo de conexao'
$shell = Rep $shell '"Decodificador TP02"' '"Decodificador de programa"' 'titulo da aba do decodificador'
$shell = Rep $shell '"Calibração de opcodes TP02"' '"Calibração de instruções"' 'titulo da aba de calibracao'
$shell = Rep $shell '"IL para Ladder - TP02"' '"IL para Ladder"' 'titulo da aba IL'
$shell = Rep $shell '"Laborat\u00F3rio de an\u00E1lise TP02"' '"Laborat\u00F3rio de an\u00E1lise de protocolo"' 'titulo da aba do laboratorio'

# ---------------------------------------------------------------- codigo morto
# Removido aqui, e nao na origem, porque a origem esta amarrada: o V128 usa
# BuildTp02HomeBarV126 e NewTp02HomeButtonV126 como ancora de inicio e fim, e o
# V52 usa a declaracao de toolValue como ancora. Como o V85 e o ultimo passo,
# nenhuma ancora posterior pode quebrar. Cada item so sai se, depois da
# remocao, o nome nao tiver mais nenhuma referencia no shell.
function Remove-Method([string]$text, [string]$signature, [string]$name) {
    $s = $text.IndexOf($signature)
    if ($s -lt 0) { throw "V85: metodo morto nao encontrado ($name)." }
    $lineStart = $text.LastIndexOf("`n", $s) + 1
    $open = $text.IndexOf('{', $s)
    $depth = 0
    $end = -1
    for ($i = $open; $i -lt $text.Length; $i++) {
        if ($text[$i] -eq '{') { $depth++ }
        elseif ($text[$i] -eq '}') { $depth--; if ($depth -eq 0) { $end = $i + 1; break } }
    }
    if ($end -lt 0) { throw "V85: fim do metodo morto nao encontrado ($name)." }
    if ($end -lt $text.Length -and $text[$end] -eq "`n") { $end++ }
    return $text.Substring(0, $lineStart) + $text.Substring($end)
}
function Remove-Field([string]$text, [string]$pattern, [string]$name) {
    $rx = New-Object System.Text.RegularExpressions.Regex('(?m)^[ \t]*' + $pattern + '[ \t]*\n')
    if (-not $rx.IsMatch($text)) { throw "V85: campo morto nao encontrado ($name)." }
    return $rx.Replace($text, '', 1)
}
function Assert-NoRefs([string]$text, [string]$name) {
    if ([regex]::IsMatch($text, '\b' + $name + '\b')) { throw "V85: $name ainda tem referencias; remocao abortada." }
}

$shell = Remove-Method $shell '        private Control BuildTp02HomeBarV126()' 'BuildTp02HomeBarV126'
$shell = Remove-Method $shell '        private Button NewTp02HomeButtonV126(' 'NewTp02HomeButtonV126'
$shell = Remove-Method $shell '        private void ShowTp02DryRun()' 'ShowTp02DryRun'
$shell = Remove-Field $shell 'private TP02PgLinkV34Form tp02ControlForm;' 'tp02ControlForm'
$shell = Remove-Field $shell 'private Label toolValue;' 'toolValue'
$shell = Remove-Field $shell 'private TextBox elementSearch;' 'elementSearch'
foreach ($dead in @('BuildTp02HomeBarV126', 'NewTp02HomeButtonV126', 'ShowTp02DryRun', 'tp02ControlForm', 'toolValue', 'elementSearch')) { Assert-NoRefs $shell $dead }

$enc = New-Object System.Text.UTF8Encoding($true)
[IO.File]::WriteAllText($shellPath, $shell, $enc)
[IO.File]::WriteAllText($uiPath, $ui, $enc)
Write-Host 'V85: menu, barra, paleta e rotulos genericos aplicados.'

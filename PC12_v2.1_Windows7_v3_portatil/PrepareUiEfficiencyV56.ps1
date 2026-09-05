$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# -----------------------------------------------------------------------------
# Toolbar: remove nome/versao duplicados. A identidade ja existe no titulo da
# janela e no painel lateral; o espaco superior fica reservado aos comandos.
# -----------------------------------------------------------------------------
$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 52;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Refazer", StudioIcon.Redo, false, delegate { InvokeLadder("Redo", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Controlador", StudioIcon.Chip, false, delegate { ShowDeviceManager(); });
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

'@
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar V56'

# Barra de status: remove a versao repetida; mantem apenas contexto operacional.
$shell = [regex]::Replace($shell,
    '                modeText\.Text = model \+ "    \|    " \+ protocol \+ "    \|    OFFLINE    \|    v[^\"]+";',
    '                modeText.Text = model + "    |    " + protocol + "    |    OFFLINE";')

# -----------------------------------------------------------------------------
# Teclado: atalhos do projeto ficam no shell. Assim Delete/Esc/Ctrl+Z/Ctrl+Y
# continuam funcionando quando o foco esta no painel lateral, e nao apenas no
# formulario Ladder hospedado.
# -----------------------------------------------------------------------------
$keyboard = @'
        private bool IsLadderActive()
        {
            try
            {
                return tabStrip != null && tabStrip.Selected != null
                    && string.Equals(tabStrip.Selected.Key, "LD", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.N))
            {
                InvokeLadder("NewProject", new object[] { true });
                return true;
            }
            if (keyData == (Keys.Control | Keys.O))
            {
                InvokeLadder("OpenProject", null);
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                InvokeLadder("SaveProject", new object[] { false });
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.S))
            {
                InvokeLadder("SaveProject", new object[] { true });
                return true;
            }

            if (IsLadderActive())
            {
                if (keyData == (Keys.Control | Keys.Z))
                {
                    InvokeLadder("Undo", null);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.Y))
                {
                    InvokeLadder("Redo", null);
                    return true;
                }
                if (keyData == Keys.Delete)
                {
                    InvokeLadder("DeleteSelectedElement", null);
                    RefreshLadderSelectionProperties();
                    return true;
                }
                if (keyData == Keys.Escape)
                {
                    SelectLadderTool(LadderTool.Select);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

'@
$menuAnchor = '        private MenuStrip BuildMenu()'
if (-not $shell.Contains('protected override bool ProcessCmdKey(ref Message msg, Keys keyData)')) {
    if (-not $shell.Contains($menuAnchor)) { throw 'BuildMenu nao encontrado para atalhos V56.' }
    $shell = $shell.Replace($menuAnchor, $keyboard + $menuAnchor)
}

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'UI V56 aplicada: toolbar sem informacao duplicada, status enxuto e atalhos globais.'

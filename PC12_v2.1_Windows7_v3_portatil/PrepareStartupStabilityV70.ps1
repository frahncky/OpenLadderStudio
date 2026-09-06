$ErrorActionPreference = 'Stop'
$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V70: UniversalStudioShell.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

# O layout moderno pode remover os labels antigos do inspector. UpdateProjectName
# nao pode assumir que projectValue exista; o catch antigo repetia a mesma
# desreferenciacao nula e derrubava o Studio durante ShowLadder() no startup.
$oldUpdate = @'
        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label label = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = label == null ? string.Empty : (label.Text ?? string.Empty).Trim();
                projectValue.Text = string.IsNullOrEmpty(value) ? "Sem nome" : value;
            }
            catch
            {
                projectValue.Text = "Projeto Ladder";
            }
        }
'@
$newUpdate = @'
        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed || projectValue == null) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label label = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = label == null ? string.Empty : (label.Text ?? string.Empty).Trim();
                projectValue.Text = string.IsNullOrEmpty(value) ? "Sem nome" : value;
            }
            catch
            {
                if (projectValue != null) projectValue.Text = "Projeto Ladder";
            }
        }
'@
if (-not $text.Contains($oldUpdate.Trim())) { throw 'V70: UpdateProjectName esperado nao encontrado.' }
$text = $text.Replace($oldUpdate.Trim(), $newUpdate.Trim())

# Guardas adicionais de startup para controles opcionais removidos pelas camadas
# de interface. Nao mudam funcionalidade; apenas evitam dereferencias nulas.
$text = $text.Replace('            inspector.Visible = true;`n            ShowDocument(ladderForm, "Programa Ladder", "LD");`n            statusText.Text = "Editor Ladder universal";', '            if (inspector != null) inspector.Visible = true;`n            ShowDocument(ladderForm, "Programa Ladder", "LD");`n            if (statusText != null) statusText.Text = "Editor Ladder universal";')

# Capabilities deve ser tratada defensivamente porque perfis customizados podem
# apontar para drivers incompletos durante a inicializacao.
$oldCaps = @'
            bool connect = currentDriver != null && currentDriver.Capabilities.Connect;
            bool monitor = currentDriver != null && (currentDriver.Capabilities.MonitorBits || currentDriver.Capabilities.ReadRegisters);
            bool readProgram = currentDriver != null && currentDriver.Capabilities.ReadProgram;
'@
$newCaps = @'
            PlcDriverCapabilities caps = currentDriver == null ? null : currentDriver.Capabilities;
            bool connect = caps != null && caps.Connect;
            bool monitor = caps != null && (caps.MonitorBits || caps.ReadRegisters);
            bool readProgram = caps != null && caps.ReadProgram;
'@
if ($text.Contains($oldCaps.Trim())) { $text = $text.Replace($oldCaps.Trim(), $newCaps.Trim()) }

# ShowDocument e ApplySelectedTab nao devem falhar se uma aba opcional vier sem
# documento durante a composicao inicial.
$text = $text.Replace('        private void ShowDocument(Form child, string title, string railCode)`n        {`n            if (child.Parent != host)', '        private void ShowDocument(Form child, string title, string railCode)`n        {`n            if (child == null || host == null || tabStrip == null) return;`n            if (child.Parent != host)')
$text = $text.Replace('                console.Write(0, "Documento aberto: " + title);', '                if (console != null) console.Write(0, "Documento aberto: " + title);')
$text = $text.Replace('            if (!tab.Document.IsDisposed)', '            if (tab.Document != null && !tab.Document.IsDisposed)')

[System.IO.File]::WriteAllText($shellPath, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V70 aplicada: startup protegido contra referencias nulas da interface.' -ForegroundColor Cyan

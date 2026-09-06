$ErrorActionPreference = 'Stop'
$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V70: UniversalStudioShell.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

# O layout moderno pode remover os labels antigos do inspector. UpdateProjectName
# nao pode assumir que projectValue exista. Substituimos o metodo pelos seus
# limites estruturais para nao depender do corpo alterado pelas camadas V52-V68.
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
$updatePattern = '(?ms)^        private void UpdateProjectName\(\).*?(?=^        private void SetRailEnabled\()'
if (-not [Regex]::IsMatch($text, $updatePattern)) { throw 'V70: limites de UpdateProjectName nao encontrados.' }
$text = [Regex]::Replace($text, $updatePattern, $newUpdate, 1)

# Guardas adicionais para controles opcionais removidos/substituidos pelo layout.
$text = [Regex]::Replace($text, '(?m)^(\s*)inspector\.Visible = true;$', '$1if (inspector != null) inspector.Visible = true;')
$text = $text.Replace('            statusText.Text = "Editor Ladder universal";', '            if (statusText != null) statusText.Text = "Editor Ladder universal";')

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

$text = $text.Replace(
    '                capabilityValue.Text = currentDriver == null ? "Driver não disponível." : currentDriver.Capabilities.Summary();',
    '                capabilityValue.Text = currentDriver == null || currentDriver.Capabilities == null ? "Driver não disponível." : currentDriver.Capabilities.Summary();')

# ShowDocument nao deve falhar se um documento opcional estiver ausente durante
# a composicao do layout.
$showDocAnchor = @'
        private void ShowDocument(Form child, string title, string railCode)
        {
'@
$showDocGuard = @'
        private void ShowDocument(Form child, string title, string railCode)
        {
            if (child == null || host == null || tabStrip == null) return;
'@
if ($text.Contains($showDocAnchor.TrimEnd()) -and -not $text.Contains('if (child == null || host == null || tabStrip == null) return;')) {
    $text = $text.Replace($showDocAnchor.TrimEnd(), $showDocGuard.TrimEnd())
}
$text = $text.Replace('                console.Write(0, "Documento aberto: " + title);', '                if (console != null) console.Write(0, "Documento aberto: " + title);')
$text = $text.Replace('            if (!tab.Document.IsDisposed)', '            if (tab.Document != null && !tab.Document.IsDisposed)')

# Assertivas objetivas: a guarda de entrada e a guarda do bloco de recuperacao
# precisam existir. Atribuicoes normais depois da guarda de entrada sao validas.
$updatedMethod = [Regex]::Match($text, '(?ms)^        private void UpdateProjectName\(\).*?(?=^        private void SetRailEnabled\()').Value
if ($updatedMethod -notmatch 'projectValue == null') { throw 'V70: guarda de projectValue nao aplicada.' }
if ($updatedMethod -notmatch 'if \(projectValue != null\) projectValue\.Text') { throw 'V70: guarda de projectValue no catch nao aplicada.' }

[System.IO.File]::WriteAllText($shellPath, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V70 aplicada: startup protegido contra referencias nulas da interface.' -ForegroundColor Cyan

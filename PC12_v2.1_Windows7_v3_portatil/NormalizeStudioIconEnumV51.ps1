$ErrorActionPreference = 'Stop'
$root = Get-Location
$path = Join-Path $root 'StudioUi.build.cs'
$auditPath = Join-Path $root 'PrepareUiAuditV51.ps1'
$resumePath = Join-Path $root 'PrepareUpdateResumeV50.ps1'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado.' }
if (-not (Test-Path $auditPath)) { throw 'PrepareUiAuditV51.ps1 nao encontrado.' }
if (-not (Test-Path $resumePath)) { throw 'PrepareUpdateResumeV50.ps1 nao encontrado.' }

# No runner Windows, os here-strings do script usam CRLF. Mantemos os arquivos-alvo
# nesse mesmo formato durante a V51 para que as ancoras multilinha sejam deterministicas.
$audit = [System.IO.File]::ReadAllText($auditPath)
$audit = [System.Text.RegularExpressions.Regex]::Replace(
    $audit,
    'function LF\(\[string\]\$text\) \{[^\r\n]*\}',
    'function LF([string]$text) { return $text }',
    1)
[System.IO.File]::WriteAllText($auditPath, $audit, [System.Text.Encoding]::UTF8)

# A V50 nasceu quando o updater ainda era mais simples. Substituimos, somente no
# workspace do build, a ancora literal por uma busca semantica tolerante a espacos
# e às demais transformacoes do atualizador moderno.
$resume = [System.IO.File]::ReadAllText($resumePath)
$resumePattern = '(?s)\$launchNeedle = @''.*?\$updater = Replace-Required \$updater \$launchNeedle \$launchReplacement ''marcador antes do instalador'''
$resumeReplacement = @'
$launchPattern = 'progress\.Value\s*=\s*100;\s*ProcessStartInfo\s+psi\s*=\s*new\s+ProcessStartInfo\(\);'
$launchReplacement = @'
                progress.Value = 100;
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Fechando o OpenLadder Studio para atualizar...";
                PrepareResumeAfterUpdate();
                Application.DoEvents();

                ProcessStartInfo psi = new ProcessStartInfo();
'@
$launchReplacement = $launchReplacement.Replace("`r`n", "`n").TrimEnd()
$updatedLaunch = [System.Text.RegularExpressions.Regex]::Replace($updater, $launchPattern, $launchReplacement, 1)
if ($updatedLaunch -eq $updater) { throw 'Ponto semantico de inicio do instalador nao encontrado.' }
$updater = $updatedLaunch
'@
$resumeNext = [System.Text.RegularExpressions.Regex]::Replace($resume, $resumePattern, $resumeReplacement.TrimEnd(), 1)
if ($resumeNext -eq $resume) { throw 'Bloco de marcador da retomada V50 nao localizado para atualizacao.' }
[System.IO.File]::WriteAllText($resumePath, $resumeNext, [System.Text.Encoding]::UTF8)

$text = [System.IO.File]::ReadAllText($path)
$canonical = @'
    internal enum StudioIcon
    {
        None, Doc, Folder, Save, Undo, Redo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid,
        Select, ContactNO, ContactNC, Coil, Timer, Counter
    }
'@
$compat = @'

    /* V51-ENUM-COMPAT
        None, Doc, Folder, Save, Undo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid
    */
'@
$pattern = '(?s)    internal enum StudioIcon\s*\{.*?\r?\n    \}'
$next = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, $canonical.TrimEnd(), 1)
if ($next -eq $text) { throw 'Enum StudioIcon nao localizado para normalizacao.' }
$next = $next + $compat
[System.IO.File]::WriteAllText($path, $next, [System.Text.Encoding]::UTF8)
Write-Host 'UI V51 e retomada V50 preparadas para o build moderno.'

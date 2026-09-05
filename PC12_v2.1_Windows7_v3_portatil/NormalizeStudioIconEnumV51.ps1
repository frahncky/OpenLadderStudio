$ErrorActionPreference = 'Stop'
$root = Get-Location
$path = Join-Path $root 'StudioUi.build.cs'
$auditPath = Join-Path $root 'PrepareUiAuditV51.ps1'
$resumePath = Join-Path $root 'PrepareUpdateResumeV50.ps1'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado.' }
if (-not (Test-Path $auditPath)) { throw 'PrepareUiAuditV51.ps1 nao encontrado.' }
if (-not (Test-Path $resumePath)) { throw 'PrepareUpdateResumeV50.ps1 nao encontrado.' }

# Mantem os arquivos-alvo no mesmo padrao CRLF dos here-strings no runner Windows.
$audit = [System.IO.File]::ReadAllText($auditPath)
$audit = [System.Text.RegularExpressions.Regex]::Replace(
    $audit,
    'function LF\(\[string\]\$text\) \{[^\r\n]*\}',
    'function LF([string]$text) { return $text }',
    1)
[System.IO.File]::WriteAllText($auditPath, $audit, [System.Text.Encoding]::UTF8)

# A V50 ainda tenta inserir o marcador pelo formato antigo. Se o AutoUpdater V36
# já o colocou no ponto estável do instalador, a substituicao passa a ser idempotente.
$resume = [System.IO.File]::ReadAllText($resumePath)
$oldRequired = @'
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
'@
$newRequired = @'
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) {
        if ($label -eq 'marcador antes do instalador') {
            if ($text.Contains('PrepareResumeAfterUpdate();')) { return $text }
            $semantic = 'progress\.Value\s*=\s*100;\s*ProcessStartInfo\s+psi\s*=\s*new\s+ProcessStartInfo\(\);'
            $match = [System.Text.RegularExpressions.Regex]::Match($text, $semantic)
            if ($match.Success) {
                return $text.Substring(0, $match.Index) + $replacement + $text.Substring($match.Index + $match.Length)
            }
        }
        throw "Ancora nao encontrada ($label)."
    }
    return $text.Replace($needle, $replacement)
}
'@
$oldRequired = $oldRequired.Replace("`r`n", "`n").TrimEnd()
$newRequired = $newRequired.Replace("`r`n", "`n").TrimEnd()
$resumeLf = $resume.Replace("`r`n", "`n")
if (-not $resumeLf.Contains($oldRequired)) { throw 'Funcao Replace-Required da V50 nao localizada.' }
$resumeLf = $resumeLf.Replace($oldRequired, $newRequired)
[System.IO.File]::WriteAllText($resumePath, $resumeLf, [System.Text.Encoding]::UTF8)

# Enum final usado pela camada visual V51. Mantemos um marcador antigo apenas para
# compatibilidade com a transformacao historica do PrepareUiAuditV51.ps1.
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

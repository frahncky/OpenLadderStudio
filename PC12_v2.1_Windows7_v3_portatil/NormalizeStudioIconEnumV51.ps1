$ErrorActionPreference = 'Stop'
$root = Get-Location
$path = Join-Path $root 'StudioUi.build.cs'
$auditPath = Join-Path $root 'PrepareUiAuditV51.ps1'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado.' }
if (-not (Test-Path $auditPath)) { throw 'PrepareUiAuditV51.ps1 nao encontrado.' }

# No runner Windows, os here-strings do script usam CRLF. Mantemos os arquivos-alvo
# nesse mesmo formato durante a V51 para que as ancoras multilinha sejam deterministicas.
$audit = [System.IO.File]::ReadAllText($auditPath)
$audit = [System.Text.RegularExpressions.Regex]::Replace(
    $audit,
    'function LF\(\[string\]\$text\) \{[^\r\n]*\}',
    'function LF([string]$text) { return $text }',
    1)
[System.IO.File]::WriteAllText($auditPath, $audit, [System.Text.Encoding]::UTF8)

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
Write-Host 'Enum StudioIcon e formato de linhas preparados para a V51.'

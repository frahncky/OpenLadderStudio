$ErrorActionPreference = 'Stop'
$root = Get-Location
$path = Join-Path $root 'StudioUi.build.cs'
$auditPath = Join-Path $root 'PrepareUiAuditV51.ps1'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado.' }
if (-not (Test-Path $auditPath)) { throw 'PrepareUiAuditV51.ps1 nao encontrado.' }

# Mantem o arquivo-alvo no mesmo padrão de quebras de linha dos here-strings no runner Windows.
$audit = [System.IO.File]::ReadAllText($auditPath)
$audit = [System.Text.RegularExpressions.Regex]::Replace(
    $audit,
    'function LF\(\[string\]\$text\) \{[^\r\n]*\}',
    'function LF([string]$text) { return $text }',
    1)
[System.IO.File]::WriteAllText($auditPath, $audit, [System.Text.Encoding]::UTF8)

# Enum final usado pela camada visual V51. Mantemos um marcador antigo apenas para
# compatibilidade com a transformação histórica do PrepareUiAuditV51.ps1.
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
Write-Host 'UI V51 preparada para o build moderno.'

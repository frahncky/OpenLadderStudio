$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'StudioUi.build.cs'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado.' }
$text = [System.IO.File]::ReadAllText($path).Replace("`r`n", "`n")
$canonical = @'
    internal enum StudioIcon
    {
        None, Doc, Folder, Save, Undo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid
    }
'@
$pattern = '(?s)    internal enum StudioIcon\s*\{.*?\n    \}'
$next = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, $canonical.TrimEnd(), 1)
if ($next -eq $text) { throw 'Enum StudioIcon nao localizado para normalizacao.' }
[System.IO.File]::WriteAllText($path, $next, [System.Text.Encoding]::UTF8)
Write-Host 'Enum StudioIcon normalizado para a etapa V51.'

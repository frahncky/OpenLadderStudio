$ErrorActionPreference = 'Stop'
$root = Get-Location
$source = Join-Path $root 'PrepareLadderZoomV74Fix2.ps1'
if (-not (Test-Path $source)) { throw 'V74 Fix3: PrepareLadderZoomV74Fix2.ps1 nao encontrado.' }

$text = [System.IO.File]::ReadAllText($source).Replace("`r`n", "`n")
$old = '$menuStart = ''        private MenuStrip BuildMenu()'''
$new = '$menuStart = ''        private void TogglePanel(int which)'''
if (-not $text.Contains($old)) { throw 'V74 Fix3: ancora de encerramento do ProcessCmdKey nao encontrada.' }
$text = $text.Replace($old, $new)
$text = $text.Replace('V72SelectLadderTool(LadderTool.Select);', 'V73SelectLadderTool(LadderTool.Select);')

$runtime = Join-Path $root 'PrepareLadderZoomV74.runtime.ps1'
try {
    [System.IO.File]::WriteAllText($runtime, $text, (New-Object System.Text.UTF8Encoding($false)))
    & $runtime
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Remove-Item $runtime -Force -ErrorAction SilentlyContinue
}

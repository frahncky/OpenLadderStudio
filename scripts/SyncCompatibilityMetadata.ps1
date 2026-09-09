param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src\OpenLadderStudio.Desktop'
# Public raw URLs embedded in already-installed versions must keep working.
$compatibilityRoot = Join-Path $repoRoot 'PC12_v2.1_Windows7_v3_portatil'

foreach ($name in @('version.txt', 'TP02-PG-Tests.json')) {
    $source = Join-Path $sourceRoot $name
    $target = Join-Path $compatibilityRoot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Canonical metadata missing: $source"
    }
    if ($Check) {
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
            throw "Compatibility metadata missing: $target. Run scripts/SyncCompatibilityMetadata.ps1."
        }
        if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            throw "Compatibility metadata differs: $name. Run scripts/SyncCompatibilityMetadata.ps1."
        }
    } else {
        New-Item -ItemType Directory -Path $compatibilityRoot -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
}
Write-Host 'Compatibility metadata synchronized with the canonical Desktop files.'

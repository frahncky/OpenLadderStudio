$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $repoRoot '.github\architecture\modules.json'
$guidePath = Join-Path $repoRoot 'docs\DEVELOPMENT_GUIDE.md'

foreach ($path in @($catalogPath, $guidePath)) {
    if (-not (Test-Path $path)) { throw "Guardrail arquitetural ausente: $path" }
}

try {
    $catalog = Get-Content -Raw -Path $catalogPath | ConvertFrom-Json
}
catch {
    throw "Catalogo de modulos invalido: $catalogPath"
}

if ($catalog.schemaVersion -ne 1) { throw 'Versao de schema arquitetural nao suportada.' }
if ([string]::IsNullOrWhiteSpace($catalog.sourceRoot)) { throw 'sourceRoot ausente no catalogo de modulos.' }
if ([string]::IsNullOrWhiteSpace($catalog.targetStructure)) { throw 'targetStructure ausente no catalogo de modulos.' }
if ($catalog.modules.Count -lt 5) { throw 'O catalogo deve declarar Core, Application, drivers e UI.' }

$moduleNames = @($catalog.modules | ForEach-Object { $_.name })
foreach ($requiredName in @(
    'OpenLadderStudio.Core',
    'OpenLadderStudio.Application',
    'OpenLadderStudio.Drivers.Modbus',
    'OpenLadderStudio.Drivers.TP02',
    'OpenLadderStudio.UI'
)) {
    if ($moduleNames -notcontains $requiredName) { throw "Modulo obrigatorio ausente: $requiredName" }
}

Write-Host 'Arquitetura: catalogo de modulos e guardrails validados.'

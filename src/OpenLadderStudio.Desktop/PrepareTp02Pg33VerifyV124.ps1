$ErrorActionPreference = 'Stop'

$core = Join-Path $PSScriptRoot 'PrepareTp02Pg33VerifyV124Core.ps1'
$v125 = Join-Path $PSScriptRoot 'PrepareTp02PgControlV125.ps1'

if (-not (Test-Path -LiteralPath $core)) { throw 'PrepareTp02Pg33VerifyV124Core.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v125)) { throw 'PrepareTp02PgControlV125.ps1 nao encontrado.' }

& $core
& $v125

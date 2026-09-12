$ErrorActionPreference = 'Stop'

$core = Join-Path $PSScriptRoot 'PrepareTp02Pg33VerifyV124Core.ps1'
$v125 = Join-Path $PSScriptRoot 'PrepareTp02PgControlV125.ps1'
$v126 = Join-Path $PSScriptRoot 'PrepareTp02HomeControlsV126.ps1'
$v127 = Join-Path $PSScriptRoot 'PrepareTp02FastPgV127.ps1'
$v128 = Join-Path $PSScriptRoot 'PrepareTp02ConnectFirstV128.ps1'
$v130 = Join-Path $PSScriptRoot 'PrepareUiHotfixV130Safe.ps1'
$v131 = Join-Path $PSScriptRoot 'PrepareTp02ConnectRobustV131.ps1'
$v132 = Join-Path $PSScriptRoot 'PrepareTp02StickyPgV132.ps1'
$v132return = Join-Path $PSScriptRoot 'PrepareTp02ReadReturnV132.ps1'
$v133 = Join-Path $PSScriptRoot 'PrepareTp02FastHomeConnectV133.ps1'
$v134 = Join-Path $PSScriptRoot 'PrepareTp02PgF0PersistenceV134.ps1'
$v135 = Join-Path $PSScriptRoot 'PrepareTp02BarVersionV135.ps1'
$v136 = Join-Path $PSScriptRoot 'PrepareTp02PgRunV136.ps1'

if (-not (Test-Path -LiteralPath $core)) { throw 'PrepareTp02Pg33VerifyV124Core.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v125)) { throw 'PrepareTp02PgControlV125.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v126)) { throw 'PrepareTp02HomeControlsV126.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v127)) { throw 'PrepareTp02FastPgV127.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v128)) { throw 'PrepareTp02ConnectFirstV128.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v130)) { throw 'PrepareUiHotfixV130Safe.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v131)) { throw 'PrepareTp02ConnectRobustV131.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v132)) { throw 'PrepareTp02StickyPgV132.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v132return)) { throw 'PrepareTp02ReadReturnV132.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v133)) { throw 'PrepareTp02FastHomeConnectV133.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v134)) { throw 'PrepareTp02PgF0PersistenceV134.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v135)) { throw 'PrepareTp02BarVersionV135.ps1 nao encontrado.' }
if (-not (Test-Path -LiteralPath $v136)) { throw 'PrepareTp02PgRunV136.ps1 nao encontrado.' }

& $core
& $v125
& $v126
& $v127
& $v128
& $v130
& $v131
& $v132
& $v132return
& $v133
& $v134
& $v136
& $v135

param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

foreach ($item in $Path) {
    if (-not (Test-Path -LiteralPath $item)) {
        throw "Arquivo C# gerado ausente: $item"
    }

    $resolved = (Resolve-Path -LiteralPath $item).Path
    $lines = [System.IO.File]::ReadAllLines($resolved)
    $seen = @{}
    $result = New-Object System.Collections.Generic.List[string]
    $removed = 0
    $header = $true

    foreach ($line in $lines) {
        if ($header -and $line -match '^using\s+[^;]+;\s*$') {
            $key = $line.Trim()
            if ($seen.ContainsKey($key)) {
                $removed++
                continue
            }
            $seen[$key] = $true
        }
        elseif ($line -match '^namespace\s+') {
            $header = $false
        }

        $result.Add($line)
    }

    if ($removed -gt 0) {
        [System.IO.File]::WriteAllLines($resolved, $result, $utf8Bom)
        Write-Host "Imports C# normalizados: $resolved ($removed duplicado(s) removido(s))."
    }
}

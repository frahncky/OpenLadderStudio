$ErrorActionPreference = 'Stop'

# V1.54 - corrige a ordem dos bytes START_H/START_L no readback PG34 multipagina.
#
# Evidencia fisica da v1.53 mostrou que o leitor gerado podia transmitir o
# endereco inicial com os bytes trocados em paginas acima de 0x00FF, embora o
# checksum continuasse valido. A v1.54 monta o pedido diretamente na rotina
# multipagina usando a ordem confirmada pelo PC12:
#   34 03 [step_hi] [step_lo] A0 chk
#
# A montagem e autocontida no shell principal para nao depender de adicionar
# outro arquivo Core ao comando de compilacao legado do Build.bat.
#
# Esta correcao e estritamente de LEITURA. Nao adiciona PG33, restore, RUN,
# STOP remoto, Clear All, 0x09 ou WBP.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V154: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$needle = '                byte[] request34 = Build34Request(startStep);'
$replacement = @'
                byte startHi34 = (byte)((startStep >> 8) & 0xFF);
                byte startLo34 = (byte)(startStep & 0xFF);
                byte[] request34 = new byte[] { 0x34, 0x03, startHi34, startLo34, 0xA0, 0x00 };
                int request34Sum = 0;
                for (int request34Index = 0; request34Index < 5; request34Index++)
                    request34Sum = (request34Sum + request34[request34Index]) & 0xFF;
                request34[5] = (byte)((0xFF - request34Sum) & 0xFF);

                int decodedStart34 = (request34[2] << 8) | request34[3];
                if (decodedStart34 != startStep)
                    throw new InvalidDataException("V154: guarda START_H/START_L rejeitou pedido PG34 antes do TX.");
'@
$count = [System.Text.RegularExpressions.Regex]::Matches(
    $shell, [System.Text.RegularExpressions.Regex]::Escape($needle)).Count
if ($count -ne 1) {
    throw "V154: chamada PG34 da v1.53 esperada exatamente uma vez; encontrado: $count."
}
$shell = $shell.Replace($needle, $replacement)

# Atualiza somente os marcadores de diagnostico da rotina multipagina.
$shell = $shell.Replace(' V153 PG34 ', ' V154 PG34 ')
$shell = $shell.Replace('PG34 PAGED READBACK v1.53', 'PG34 PAGED READBACK v1.54')
$shell = $shell.Replace('-pg34-paged-v153.txt', '-pg34-paged-v154.txt')
$shell = $shell.Replace('V153: porta PG fechada antes do readback multipagina.',
    'V154: porta PG fechada antes do readback multipagina.')

# Guardas de build: a rotina corrigida deve conter explicitamente HIGH primeiro,
# LOW depois, e deve validar o endereco recomposto antes de qualquer TX.
$required = @(
    'byte startHi34 = (byte)((startStep >> 8) & 0xFF);',
    'byte startLo34 = (byte)(startStep & 0xFF);',
    'int decodedStart34 = (request34[2] << 8) | request34[3];',
    'V154: guarda START_H/START_L rejeitou pedido PG34 antes do TX.'
)
foreach ($token in $required) {
    if (-not $shell.Contains($token)) {
        throw "V154: guarda de build falhou; token ausente: $token"
    }
}

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG34 Endian Fix V154 aplicado: START_H/START_L explicitos, guarda antes do TX e VERIFY read-only.'

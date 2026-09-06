$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'PLCMemoryMapManager.cs'
$outputPath = Join-Path (Get-Location) 'PLCMemoryMapManagerV15.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('a.Length = ParseNumber(Cell(row, "length", "1"), 1, 2000, "Tamanho");', @'
a.Length = ParseNumber(Cell(row, "length", "1"), 1, 65536, "Tamanho");
                    if ((long)a.StartAddress + (long)a.Length > 65536L)
                        throw new InvalidOperationException("A área '" + a.Name + "' ultrapassa o endereço Modbus 65535.");
'@.TrimEnd())

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)

# A auditoria visual V51 roda imediatamente antes deste passo no BUILD_INTERFACE_MODERNA.bat.
# Aplicamos a composicao V68 depois dela para que o tema aprovado seja a ultima camada de UI.
$v68 = Join-Path (Get-Location) 'PrepareVisualStudioV68.ps1'
if (-not (Test-Path $v68)) { throw 'PrepareVisualStudioV68.ps1 nao encontrado.' }
& $v68

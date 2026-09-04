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

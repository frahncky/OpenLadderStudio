$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$text = [System.IO.File]::ReadAllText($path)

$fieldNeedle = '        private TP02BridgeForm bridgeForm;'
$fieldReplacement = "        private TP02BridgeForm bridgeForm;`r`n        private TP02ControlForm tp02ControlForm;"
if (-not $text.Contains($fieldNeedle)) { throw 'Campo bridgeForm não encontrado no shell gerado.' }
$text = $text.Replace($fieldNeedle, $fieldReplacement)

$startAnchor = '        private void ShowCommunication()'
$endAnchor = '        private void ShowMonitor()'
$start = $text.IndexOf($startAnchor)
if ($start -lt 0) { throw 'Método ShowCommunication não encontrado no shell gerado.' }
$end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
if ($end -lt 0) { throw 'Método ShowMonitor não encontrado após ShowCommunication.' }

$section = $text.Substring($start, $end - $start)
$oldCreate = 'if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();'
$oldShow = 'ShowDocument(bridgeForm, "Comunicação - WEG TP02", "PLC");'
$oldStatus = 'statusText.Text = "Driver WEG TP02";'
if (-not $section.Contains($oldCreate)) { throw 'Criação do TP02BridgeForm não encontrada em ShowCommunication.' }
if (-not $section.Contains($oldShow)) { throw 'ShowDocument do TP02BridgeForm não encontrado em ShowCommunication.' }
if (-not $section.Contains($oldStatus)) { throw 'Status do driver TP02 não encontrado em ShowCommunication.' }

$section = $section.Replace($oldCreate, 'if (tp02ControlForm == null || tp02ControlForm.IsDisposed) tp02ControlForm = new TP02ControlForm();')
$section = $section.Replace($oldShow, 'ShowDocument(tp02ControlForm, "Controle online - WEG TP02", "PLC");')
$section = $section.Replace($oldStatus, 'statusText.Text = "Controle WEG TP02: leitura, escrita e RUN/STOP";')
$text = $text.Substring(0, $start) + $section + $text.Substring($end)

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)

$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$text = [System.IO.File]::ReadAllText($path)

$fieldNeedle = '        private TP02BridgeForm bridgeForm;'
$fieldReplacement = "        private TP02BridgeForm bridgeForm;`r`n        private TP02ControlForm tp02ControlForm;"
if (-not $text.Contains($fieldNeedle)) { throw 'Campo bridgeForm nao encontrado no shell gerado.' }
$text = $text.Replace($fieldNeedle, $fieldReplacement)

$startAnchor = '        private void ShowCommunication()'
$endAnchor = '        private void ShowMonitor()'
$start = $text.IndexOf($startAnchor)
if ($start -lt 0) { throw 'Metodo ShowCommunication nao encontrado no shell gerado.' }
$end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
if ($end -lt 0) { throw 'Metodo ShowMonitor nao encontrado apos ShowCommunication.' }

$section = $text.Substring($start, $end - $start)
$oldCreate = 'if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();'
$oldShowPrefix = 'ShowDocument(bridgeForm,'
$oldStatus = 'statusText.Text = "Driver WEG TP02";'
if (-not $section.Contains($oldCreate)) { throw 'Criacao do TP02BridgeForm nao encontrada em ShowCommunication.' }
if (-not $section.Contains($oldShowPrefix)) { throw 'ShowDocument do TP02BridgeForm nao encontrado em ShowCommunication.' }
if (-not $section.Contains($oldStatus)) { throw 'Status do driver TP02 nao encontrado em ShowCommunication.' }

$section = $section.Replace($oldCreate, 'if (tp02ControlForm == null || tp02ControlForm.IsDisposed) tp02ControlForm = new TP02ControlForm();')
$section = $section.Replace($oldShowPrefix, 'ShowDocument(tp02ControlForm,')
$section = $section.Replace($oldStatus, 'statusText.Text = "Controle WEG TP02: leitura, escrita e RUN/STOP";')
$text = $text.Substring(0, $start) + $section + $text.Substring($end)

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)

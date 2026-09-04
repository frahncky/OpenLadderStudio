$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$text = [System.IO.File]::ReadAllText($path)

$fieldNeedle = '        private TP02BridgeForm bridgeForm;'
$fieldReplacement = @'
        private TP02BridgeForm bridgeForm;
        private TP02ControlForm tp02ControlForm;
'@
if (-not $text.Contains($fieldNeedle)) { throw 'Campo bridgeForm não encontrado no shell gerado.' }
$text = $text.Replace($fieldNeedle, $fieldReplacement.TrimEnd())

$oldBlock = @'
            if (IsTp02())
            {
                if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
                inspector.Visible = false;
                ShowDocument(bridgeForm, "Comunicação - WEG TP02", "PLC");
                statusText.Text = "Driver WEG TP02";
                return;
            }
'@
$newBlock = @'
            if (IsTp02())
            {
                if (tp02ControlForm == null || tp02ControlForm.IsDisposed) tp02ControlForm = new TP02ControlForm();
                inspector.Visible = false;
                ShowDocument(tp02ControlForm, "Controle online - WEG TP02", "PLC");
                statusText.Text = "Controle WEG TP02: leitura, escrita e RUN/STOP";
                return;
            }
'@
if (-not $text.Contains($oldBlock.Trim())) { throw 'Bloco TP02 de ShowCommunication não encontrado no shell gerado.' }
$text = $text.Replace($oldBlock.Trim(), $newBlock.Trim())

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)

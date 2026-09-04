$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'UniversalStudioShell.cs'
$outputPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

# String.Replace devolve o texto intacto quando a agulha nao existe, entao uma
# mudanca no shell faria recursos da v0.18 sumirem do build em silencio.
# Toda substituicao passa por aqui e falha alto se a agulha nao for encontrada.
function Invoke-Replace([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) {
        throw "Ancora nao encontrada em UniversalStudioShell.cs ($label). Ajuste PrepareUniversalStudioV18.ps1."
    }
    return $haystack.Replace($needle, $replacement)
}

$text = Invoke-Replace $text 'v0.12' 'v0.18' 'versao'

$menuNeedle = '            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));'
$menuInsert = @'
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(DropItem("Mapa de memória...", delegate { ShowMemoryMapManager(); }));
'@
$text = Invoke-Replace $text $menuNeedle $menuInsert.TrimEnd() 'menu PLC'

$toolbarNeedle = '            AddToolButton(bar, "Comunicação", StudioIcon.Plug, false, delegate { ShowCommunication(); });'
$toolbarInsert = @'
            AddToolButton(bar, "Comunicação", StudioIcon.Plug, false, delegate { ShowCommunication(); });
            AddToolButton(bar, "Mapa", StudioIcon.Grid, false, delegate { ShowMemoryMapManager(); });
'@
$text = Invoke-Replace $text $toolbarNeedle $toolbarInsert.TrimEnd() 'barra de ferramentas'

$changeNeedle = @'
            Button change = InspectorButton("Trocar controlador", 16, 500, 238);
            change.Click += delegate { ShowDeviceManager(); };
            p.Controls.Add(change);
'@
$changeInsert = @'
            Button change = InspectorButton("Trocar controlador", 16, 500, 238);
            change.Click += delegate { ShowDeviceManager(); };
            p.Controls.Add(change);

            Button memoryMap = InspectorButton("Mapa de memória", 16, 542, 238);
            memoryMap.Click += delegate { ShowMemoryMapManager(); };
            p.Controls.Add(memoryMap);
'@
$text = Invoke-Replace $text $changeNeedle.Trim() $changeInsert.Trim() 'inspetor'

$methodNeedle = '        private void ShowCommunication()'
$methodInsert = @'
        private void ShowMemoryMapManager()
        {
            using (PlcMemoryMapManagerForm dialog = new PlcMemoryMapManagerForm())
            {
                dialog.ShowDialog(this);
            }
            RefreshProfileUi();
            statusText.Text = currentProfile == null ? "Mapa de memória atualizado" : "Mapa de memória: " + currentProfile.Manufacturer + " " + currentProfile.Model;
        }

'@
$text = Invoke-Replace $text $methodNeedle ($methodInsert + $methodNeedle) 'ShowCommunication'

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)

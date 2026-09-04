$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'UniversalStudioShell.cs'
$outputPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('v0.12', 'v0.14')

$menuNeedle = '            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));'
$menuInsert = @'
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(DropItem("Mapa de memória...", delegate { ShowMemoryMapManager(); }));
'@
$text = $text.Replace($menuNeedle, $menuInsert.TrimEnd())

$toolbarNeedle = '            bar.Items.Add(ToolButton("Comunicação", delegate { ShowCommunication(); }));'
$toolbarInsert = @'
            bar.Items.Add(ToolButton("Comunicação", delegate { ShowCommunication(); }));
            bar.Items.Add(ToolButton("Mapa", delegate { ShowMemoryMapManager(); }));
'@
$text = $text.Replace($toolbarNeedle, $toolbarInsert.TrimEnd())

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
$text = $text.Replace($changeNeedle.Trim(), $changeInsert.Trim())

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
$text = $text.Replace($methodNeedle, $methodInsert + $methodNeedle)

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)

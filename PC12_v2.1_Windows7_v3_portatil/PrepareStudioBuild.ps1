$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'PC12DirectStudio.cs'
$outputPath = Join-Path (Get-Location) 'PC12DirectStudio.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$oldPlcMenu = '            plc.DropDownItems.Add(DropItem("Comunicação", delegate { ShowBridge(); }));'
$newPlcMenu = @'
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(DropItem("Monitor Modbus RTU/TCP...", delegate { ShowModbus(); }));
            plc.DropDownItems.Add(new ToolStripSeparator());
            plc.DropDownItems.Add(DropItem("Comunicação TP02", delegate { ShowBridge(); }));
'@
$text = $text.Replace($oldPlcMenu, $newPlcMenu.TrimEnd())

$oldToolbar = '            bar.Items.Add(ToolButton("Ler PLC", delegate { ShowReader(); }));'
$newToolbar = @'
            bar.Items.Add(ToolButton("Ler PLC", delegate { ShowReader(); }));
            bar.Items.Add(ToolButton("Modbus", delegate { ShowModbus(); }));
'@
$text = $text.Replace($oldToolbar, $newToolbar.TrimEnd())

$oldAbout = 'Programação Ladder e ferramentas para WEG TP02.'
$newAbout = 'Ambiente Ladder multi-fabricante. WEG TP02 é o primeiro driver nativo; Modbus RTU/TCP genérico está disponível para monitoramento.'
$text = $text.Replace($oldAbout, $newAbout)

$oldDevice = '            Label deviceValue = InspectorLabel("WEG TP02-60MR", 9.2f, true, Fore);'
$newDevice = @'
            PlcDeviceProfile currentDevice = PlcProfileStore.Load();
            string currentDeviceName = currentDevice == null ? "Nenhum controlador" : currentDevice.Manufacturer + " " + currentDevice.Model;
            Label deviceValue = InspectorLabel(currentDeviceName, 9.2f, true, Fore);
            deviceValue.Name = "openladder_current_device";
'@
$text = $text.Replace($oldDevice, $newDevice.TrimEnd())

$oldMode = '            modeText.Text = "TP02-60MR    |    OFFLINE    |    v0.11";'
$newMode = @'
            PlcDeviceProfile footerDevice = PlcProfileStore.Load();
            string footerModel = footerDevice == null ? "SEM PLC" : footerDevice.Model;
            modeText.Text = footerModel + "    |    OFFLINE    |    v0.11";
'@
$text = $text.Replace($oldMode, $newMode.TrimEnd())

$marker = '        private void ShowUpdater()'
$methods = @'
        private void ShowDeviceManager()
        {
            using (PlcDeviceManagerForm dialog = new PlcDeviceManagerForm())
            {
                dialog.ShowDialog(this);
            }

            PlcDeviceProfile profile = PlcProfileStore.Load();
            if (profile != null)
            {
                Control[] labels = Controls.Find("openladder_current_device", true);
                if (labels.Length > 0) labels[0].Text = profile.Manufacturer + " " + profile.Model;
                if (modeText != null) modeText.Text = profile.Model + "    |    OFFLINE    |    v0.11";
                if (statusText != null) statusText.Text = "Controlador selecionado: " + profile.Manufacturer + " " + profile.Model;
            }
        }

        private void ShowModbus()
        {
            ModbusMonitorForm monitor = new ModbusMonitorForm();
            monitor.Show(this);
            if (statusText != null) statusText.Text = "Monitor Modbus RTU/TCP";
        }

'@
$text = $text.Replace($marker, $methods + $marker)

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)

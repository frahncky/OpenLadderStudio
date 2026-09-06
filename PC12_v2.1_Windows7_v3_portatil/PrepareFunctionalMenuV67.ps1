$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

# Menu Ferramentas: deixa no nivel principal apenas funcoes de uso cotidiano.
# Diagnostico avancado TP02 recebe somente ferramentas tecnicas que ainda fazem
# sentido durante a validacao do protocolo. Calibracao e IL->Ladder deixam de
# aparecer porque sao recursos internos/incompletos no fluxo atual.
$shell = $shell.Replace('tp02.DropDownItems.Add(DropItem("Testar link de programa\u00E7\u00E3o (PG)", delegate { LaunchInstalledTool("OpenLadderTP02PgLab.exe", "Laborat\u00F3rio PG TP02"); }));', 'tp02.DropDownItems.Add(DropItem("Testar comunica\u00E7\u00E3o PG", delegate { LaunchTp02Tool("OpenLadderTP02PgLab.exe", "Laborat\u00F3rio PG TP02"); }));')
$shell = $shell.Replace('tp02.DropDownItems.Add(DropItem("Analisar projeto/serial PC12", delegate { ShowTp02BridgeLab(); }));', 'tp02.DropDownItems.Add(DropItem("An\u00E1lise PC12/TP02", delegate { ShowTp02BridgeLab(); }));')
$shell = $shell.Replace('tp02.DropDownItems.Add(DropItem("Capturar tr\u00E1fego serial PC12/TP02", delegate { LaunchInstalledTool("OpenLadderTP02Capture.exe", "Captura serial PC12/TP02"); }));', 'tp02.DropDownItems.Add(DropItem("Captura serial PC12/TP02", delegate { LaunchTp02Tool("OpenLadderTP02Capture.exe", "Captura serial PC12/TP02"); }));')
$shell = $shell.Replace('tp02.DropDownItems.Add(DropItem("Decodificar RBP", delegate { ShowDecoder(); }));', 'tp02.DropDownItems.Add(DropItem("Decodificador RBP", delegate { ShowDecoder(); }));')
$shell = $shell.Replace('            tp02.DropDownItems.Add(DropItem("Calibrar opcodes", delegate { ShowCalibration(); }));' + "`n", '')
$shell = $shell.Replace('            tp02.DropDownItems.Add(DropItem("Converter IL para Ladder", delegate { ShowIl(); }));' + "`n", '')

# Centro de conexao do TP02 usa a mesma nomenclatura clara do menu avancado.
$shell = $shell.Replace('pg.Text = "Testar link de programa\u00E7\u00E3o (PG)";', 'pg.Text = "Testar comunica\u00E7\u00E3o PG";')

# Ferramentas externas avancadas so podem ser abertas com TP02 ativo. Evita que
# diagnosticos de fabricante sejam disparados enquanto outro controlador esta selecionado.
$anchor = '        private void LaunchInstalledTool(string exeName, string friendlyName)'
if (-not $shell.Contains('private void LaunchTp02Tool(string exeName, string friendlyName)')) {
    if (-not $shell.Contains($anchor)) { throw 'LaunchInstalledTool nao encontrado.' }
    $helper = @'
        private void LaunchTp02Tool(string exeName, string friendlyName)
        {
            if (!RequireTp02("Esta ferramenta de diagn\u00F3stico \u00E9 espec\u00EDfica do WEG TP02.")) return;
            LaunchInstalledTool(exeName, friendlyName);
        }

'@
    $shell = $shell.Replace($anchor, $helper + $anchor)
}

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'V67 aplicada: menu Ferramentas enxuto e diagnostico TP02 separado de recursos internos.'

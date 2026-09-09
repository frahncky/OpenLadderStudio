$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V82: UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)

# O shell gerado tem fim de linha MISTO: PrepareUpdateNotification normaliza o
# texto para LF, e scripts posteriores inserem trechos com [Environment]::NewLine,
# que e CRLF. Escolher uma convencao para o arquivo inteiro faz a ancora
# multilinha falhar conforme o trecho em que ela cai, entao cada ancora e
# tentada nas duas formas.
function Replace-Required([string]$body, [string]$needle, [string]$replacement, [string]$label) {
    $lf = $needle.Replace("`r`n", "`n")
    $crlf = $lf.Replace("`n", "`r`n")
    $vLf = $replacement.Replace("`r`n", "`n")
    $vCrlf = $vLf.Replace("`n", "`r`n")

    if ($body.Contains($crlf)) { return $body.Replace($crlf, $vCrlf) }
    if ($body.Contains($lf)) { return $body.Replace($lf, $vLf) }
    throw "V82: ancora nao encontrada ($label)."
}

if ($shell.Contains('private ToolStripMenuItem tp02Tools')) {
    Write-Host 'OpenLadder Studio: menu por modelo V82 ja aplicado.' -ForegroundColor DarkGray
    return
}

# ---------------------------------------------------------------------------
# O submenu "Diagnostico avancado TP02" aparecia sempre, com qualquer
# controlador ativo. Cada item ja recusava abrir fora do TP02, mas so depois do
# clique: o menu prometia uma ferramenta que a proxima janela negava. Com o PLC
# virtual como perfil inicial isso ficou pior ainda -- o produto abria
# oferecendo diagnostico de um fabricante que o usuario nem escolheu.
#
# Agora o submenu segue o modelo: ele existe quando o controlador ativo e um
# TP02 e some quando nao e. A recusa por RequireTp02 continua no lugar, como
# rede de seguranca para quem trocar de perfil com a janela aberta.
# ---------------------------------------------------------------------------
$shell = Replace-Required $shell `
    '            ToolStripMenuItem tp02 = MenuItem("Diagnóstico avançado TP02");' `
    '            tp02Tools = MenuItem("Diagnóstico avançado TP02");
            ToolStripMenuItem tp02 = tp02Tools;' `
    'submenu TP02 guardado em campo'

$shell = Replace-Required $shell `
    '        private bool IsTp02()' `
    '        private ToolStripMenuItem tp02Tools;

        /// <summary>Mostra o diagnostico de fabricante so quando o modelo ativo e dele.</summary>
        private void RefreshToolsForProfile()
        {
            if (tp02Tools != null) tp02Tools.Visible = IsTp02();
        }

        private bool IsTp02()' `
    'campo e atualizacao do menu'

$shell = Replace-Required $shell `
    '            currentProfile = PlcProfileStore.Load();
            currentDriver = currentProfile == null ? null : PlcDriverRegistry.FindDriver(currentProfile.DriverId);' `
    '            currentProfile = PlcProfileStore.Load();
            currentDriver = currentProfile == null ? null : PlcDriverRegistry.FindDriver(currentProfile.DriverId);
            RefreshToolsForProfile();' `
    'chamada na troca de perfil'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'OpenLadder Studio: menu por modelo V82 aplicado (diagnostico TP02 segue o controlador ativo).' -ForegroundColor Cyan

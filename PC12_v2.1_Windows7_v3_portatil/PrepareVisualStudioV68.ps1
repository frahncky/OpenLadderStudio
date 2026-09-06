$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'
$versionPath = Join-Path $root 'version.txt'

if (-not (Test-Path $shellPath)) { throw 'V68: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $uiPath)) { throw 'V68: StudioUi.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)
$ui = [System.IO.File]::ReadAllText($uiPath)
$version = if (Test-Path $versionPath) { [System.IO.File]::ReadAllText($versionPath).Trim() } else { '0.67' }

function Set-ThemeColor([string]$text, [string]$scope, [string]$name, [string]$rgb) {
    $pattern = '(?m)^(\s*' + [Regex]::Escape($scope) + '\s+Color\s+' + [Regex]::Escape($name) + '\s*=\s*)Color\.FromArgb\([^;]+\);'
    return [Regex]::Replace($text, $pattern, ('$1Color.FromArgb(' + $rgb + ');'))
}

# Tema claro aprovado: fundo branco/cinza muito claro, azul institucional e texto escuro.
$theme = @{
    Shell       = '248, 250, 253'
    Chrome      = '255, 255, 255'
    ChromeLight = '238, 244, 252'
    Border      = '207, 218, 230'
    Accent      = '28, 105, 210'
    AccentDark  = '18, 78, 160'
    Workspace   = '250, 252, 255'
    Fore        = '30, 44, 62'
    Muted       = '83, 101, 122'
    NavBg       = '245, 248, 252'
    NavHover    = '235, 242, 250'
    NavActive   = '224, 236, 251'
    Faint       = '111, 129, 149'
    Disabled    = '160, 172, 185'
}

foreach ($name in $theme.Keys) {
    $ui = Set-ThemeColor $ui 'public static readonly' $name $theme[$name]
    $shell = Set-ThemeColor $shell 'private readonly' $name $theme[$name]
}

# Cores auxiliares que aparecem fora das propriedades centrais do tema.
$lightPairs = @(
    @('Color.FromArgb(22, 24, 27)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(16, 22, 29)', 'Color.FromArgb(245, 248, 252)'),
    @('Color.FromArgb(20, 27, 35)', 'Color.FromArgb(245, 248, 252)'),
    @('Color.FromArgb(31, 41, 52)', 'Color.FromArgb(235, 242, 250)'),
    @('Color.FromArgb(34, 46, 58)', 'Color.FromArgb(224, 236, 251)'),
    @('Color.FromArgb(10, 31, 46)', 'Color.FromArgb(248, 250, 253)'),
    @('Color.FromArgb(14, 42, 61)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(24, 58, 79)', 'Color.FromArgb(238, 244, 252)'),
    @('Color.FromArgb(48, 76, 94)', 'Color.FromArgb(207, 218, 230)'),
    @('Color.FromArgb(168, 174, 181)', 'Color.FromArgb(120, 135, 151)'),
    @('Color.FromArgb(171, 181, 191)', 'Color.FromArgb(73, 92, 113)')
)
foreach ($pair in $lightPairs) {
    $ui = $ui.Replace($pair[0], $pair[1])
    $shell = $shell.Replace($pair[0], $pair[1])
}

# Identidade coerente com version.txt.
$shell = [Regex]::Replace($shell, 'OpenLadder Studio\s+v\d+\.\d+', ('OpenLadder Studio  v' + $version))

# Toolbar mais proxima do conceito aprovado, preservando os handlers existentes.
$shell = $shell.Replace('AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });', 'AddToolButton(bar, "Compilar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });')
$shell = $shell.Replace('AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });', 'AddToolButton(bar, "Transferir", StudioIcon.Download, true, delegate { ShowCommunication(); });')
$shell = $shell.Replace('AddToolButton(bar, "Comunicação", StudioIcon.Plug, false, delegate { ShowCommunication(); });', 'AddToolButton(bar, "Transferir", StudioIcon.Download, false, delegate { ShowCommunication(); });')

$monitorLine = '            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });'
if ($shell.Contains($monitorLine) -and -not $shell.Contains('"Simulador", StudioIcon.Bolt')) {
    $shell = $shell.Replace($monitorLine, $monitorLine + [Environment]::NewLine + '            AddToolButton(bar, "Simulador", StudioIcon.Bolt, false, delegate { ShowSimulator(); });')
}

$shell = $shell.Replace('bar.Height = 48;', 'bar.Height = 72;')
$shell = $shell.Replace('bar.Height = 60;', 'bar.Height = 72;')
$shell = $shell.Replace('b.Height = 40;', 'b.Height = 64;')
$shell = $shell.Replace('b.Height = 54;', 'b.Height = 64;')
$shell = $shell.Replace('b.Location = new Point(toolCursor, 1);', 'b.Location = new Point(toolCursor, 4);')
$shell = $shell.Replace('b.Location = new Point(toolCursor, 3);', 'b.Location = new Point(toolCursor, 4);')
$shell = $shell.Replace('sep.Bounds = new Rectangle(toolCursor + 7, 10, 1, 28);', 'sep.Bounds = new Rectangle(toolCursor + 7, 18, 1, 34);')
$shell = $shell.Replace('sep.Bounds = new Rectangle(toolCursor + 7, 15, 1, 30);', 'sep.Bounds = new Rectangle(toolCursor + 7, 18, 1, 34);')

# A auditoria V51 ja fornece uma paleta vetorial colorida por tipo de icone.
# Aqui apenas garantimos que os rotulos usem o texto escuro do tema claro.
$ui = $ui.Replace('Emphasis ? StudioTheme.Accent : StudioTheme.Muted,', 'Emphasis ? StudioTheme.Accent : StudioTheme.Fore,')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)
Write-Host ('OpenLadder Studio v' + $version + ': interface visual clara aplicada.') -ForegroundColor Cyan

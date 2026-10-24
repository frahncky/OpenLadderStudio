# Renderiza a janela principal (ou outro Form) do OpenLadderStudio.exe ja compilado
# em PNG, sem interacao, para conferencia visual de tema, layout e simbolos Ladder.
# Uso: powershell -ExecutionPolicy Bypass -File scripts/RenderUi.ps1 -Theme dark -Width 1500 -Height 900 -Out C:\tmp\shell-dark.png
#      -Form ModernPC12.TP02ProgramReaderForm   renderiza um Form com construtor sem argumentos
param(
    [ValidateSet('dark', 'light')][string]$Theme = 'dark',
    [int]$Width = 1500,
    [int]$Height = 900,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Form = 'ModernPC12.UniversalStudioForm',
    [string]$Exe
)
$ErrorActionPreference = 'Stop'
if (-not $Exe) { $Exe = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\src\OpenLadderStudio.Desktop\OpenLadderStudio.exe' }
if (-not (Test-Path -LiteralPath $Exe)) { throw "Executavel nao encontrado: $Exe. Rode o Build.bat antes." }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
$static = [Reflection.BindingFlags]'Static,NonPublic,Public'
$inst = [Reflection.BindingFlags]'Instance,NonPublic,Public'

# Fixa o tema sem ler a preferencia salva do usuario.
$palette = $asm.GetType('ModernPC12.OpenLadderPalette', $true)
$modeType = $asm.GetType('ModernPC12.OpenLadderThemeMode', $true)
$palette.GetField('loaded', $static).SetValue($null, $true)
$palette.GetField('mode', $static).SetValue($null, [Enum]::Parse($modeType, $(if ($Theme -eq 'light') { 'Light' } else { 'Dark' })))

$type = $asm.GetType($Form, $true)
$window = [Activator]::CreateInstance($type, $true)

# Na janela principal, preenche dois rungs de exemplo para exibir os simbolos.
$ladderField = $type.GetField('ladderForm', $inst)
if ($ladderField) {
    $ladder = $ladderField.GetValue($window)
    $canvas = $ladder.GetType().GetField('canvas', $inst).GetValue($ladder)
    $elementType = $asm.GetType('ModernPC12.LadderElementType', $true)
    $rungsField = $canvas.GetType().GetField('Rungs', $inst)
    $rungs = if ($rungsField) { $rungsField.GetValue($canvas) } else { $canvas.GetType().GetProperty('Rungs', $inst).GetValue($canvas) }
    function Set-Element($element, $kind, $address, $parameter) {
        $element.Type = [Enum]::Parse($elementType, $kind); $element.Address = $address; $element.Parameter = $parameter
    }
    Set-Element $rungs[0].Elements[0] ContactNO X0001 ''
    Set-Element $rungs[0].Elements[1] ContactNC X0002 ''
    Set-Element $rungs[0].Elements[4] Timer T01 K50
    Set-Element $rungs[0].Elements[7] Coil Y0001 ''
    Set-Element $rungs[1].Elements[0] ContactNO X0003 ''
    Set-Element $rungs[1].Elements[7] Set Y0002 ''
    $canvas.SelectedRung = 0; $canvas.SelectedColumn = 4
}

$window.StartPosition = 'Manual'
$window.Location = New-Object Drawing.Point(-3000, 0)
$window.Size = New-Object Drawing.Size($Width, $Height)
$window.Show()
for ($k = 0; $k -lt 6; $k++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }

$bitmap = New-Object Drawing.Bitmap($window.Width, $window.Height)
$window.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0, 0, $window.Width, $window.Height)))
$bitmap.Save($Out)
Write-Output "Render salvo: $Out ($Theme, $($window.Width)x$($window.Height))"
[Environment]::Exit(0)

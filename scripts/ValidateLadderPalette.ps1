param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\src\OpenLadderStudio.Desktop\OpenLadderStudio.exe')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ExecutablePath))
$form = [Activator]::CreateInstance($assembly.GetType('ModernPC12.UniversalStudioForm', $true), $true)

function Get-DescendantControls([System.Windows.Forms.Control]$Control) {
    $Control
    foreach ($child in $Control.Controls) { Get-DescendantControls $child }
}

try {
    $controls = @(Get-DescendantControls $form)
    $selector = @($controls | Where-Object { $_.Text -eq 'Selecionar / mover' })
    if ($selector.Count -ne 1) { throw 'Expected exactly one selection tool in the ladder palette.' }

    $palette = $selector[0].Parent
    $editTitle = 'EDI' + [char]0x00C7 + [char]0x00C3 + 'O'
    $sections = @($palette.Controls | Where-Object {
        $_ -is [System.Windows.Forms.Label] -and $_.Text -eq $editTitle
    })
    if ($sections.Count -ne 1) { throw "Expected one edit section in the runtime palette; found $($sections.Count)." }
    if ($palette.Controls.IndexOf($sections[0]) -ne 0 -or $palette.Controls.IndexOf($selector[0]) -ne 1) {
        throw 'The edit section and selection tool must be the first entries in the palette.'
    }
    $contacts = @($palette.Controls | Where-Object { $_.Text -eq 'CONTATOS' })
    if ($contacts.Count -ne 1 -or $palette.Controls.IndexOf($contacts[0]) -le $palette.Controls.IndexOf($selector[0])) {
        throw 'The instruction library must follow the edit tools.'
    }
    Write-Host 'Ladder palette validated at runtime: one edit section, selection at top, library after editing tools.'
}
finally { $form.Dispose() }

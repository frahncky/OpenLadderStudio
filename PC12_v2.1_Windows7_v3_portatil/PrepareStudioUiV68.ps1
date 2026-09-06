$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'StudioUi.build.cs'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado. Execute PrepareStudioUiV21.ps1 antes.' }
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) { throw "Ancora nao encontrada em StudioUi.build.cs ($label)." }
    return $haystack.Replace($needle, $replacement)
}

# V68: shell industrial claro. A cor continua semantica nos icones e estados,
# enquanto menus, abas, console e areas auxiliares passam a usar superficies claras.
$text = Replace-Required $text '        public static readonly Color Shell = Color.FromArgb(18, 24, 31);' '        public static readonly Color Shell = Color.FromArgb(247, 249, 252);' 'Shell'
$text = Replace-Required $text '        public static readonly Color Chrome = Color.FromArgb(27, 36, 46);' '        public static readonly Color Chrome = Color.FromArgb(255, 255, 255);' 'Chrome'
$text = Replace-Required $text '        public static readonly Color ChromeLight = Color.FromArgb(38, 49, 62);' '        public static readonly Color ChromeLight = Color.FromArgb(235, 243, 252);' 'ChromeLight'
$text = Replace-Required $text '        public static readonly Color Border = Color.FromArgb(55, 68, 82);' '        public static readonly Color Border = Color.FromArgb(207, 216, 226);' 'Border'
$text = Replace-Required $text '        public static readonly Color Accent = Color.FromArgb(38, 166, 154);' '        public static readonly Color Accent = Color.FromArgb(28, 112, 220);' 'Accent'
$text = Replace-Required $text '        public static readonly Color AccentDark = Color.FromArgb(28, 128, 119);' '        public static readonly Color AccentDark = Color.FromArgb(18, 70, 126);' 'AccentDark'
$text = Replace-Required $text '        public static readonly Color Workspace = Color.FromArgb(244, 247, 250);' '        public static readonly Color Workspace = Color.FromArgb(247, 249, 252);' 'Workspace'
$text = Replace-Required $text '        public static readonly Color Fore = Color.FromArgb(226, 230, 234);' '        public static readonly Color Fore = Color.FromArgb(31, 43, 56);' 'Fore'
$text = Replace-Required $text '        public static readonly Color Muted = Color.FromArgb(150, 157, 164);' '        public static readonly Color Muted = Color.FromArgb(93, 108, 124);' 'Muted'
$text = Replace-Required $text '        public static readonly Color NavBg = Color.FromArgb(20, 27, 35);' '        public static readonly Color NavBg = Color.FromArgb(247, 249, 252);' 'NavBg'
$text = Replace-Required $text '        public static readonly Color NavHover = Color.FromArgb(31, 41, 52);' '        public static readonly Color NavHover = Color.FromArgb(236, 244, 252);' 'NavHover'
$text = Replace-Required $text '        public static readonly Color NavActive = Color.FromArgb(34, 46, 58);' '        public static readonly Color NavActive = Color.FromArgb(224, 238, 252);' 'NavActive'
$text = Replace-Required $text '        public static readonly Color Faint = Color.FromArgb(108, 116, 124);' '        public static readonly Color Faint = Color.FromArgb(125, 138, 151);' 'Faint'
$text = Replace-Required $text '        public static readonly Color Disabled = Color.FromArgb(92, 97, 103);' '        public static readonly Color Disabled = Color.FromArgb(165, 173, 181);' 'Disabled'
$text = Replace-Required $text '            BackColor = Color.FromArgb(17, 23, 30);' '            BackColor = Color.FromArgb(255, 255, 255);' 'console'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'Studio UI V68 clara aplicada.'

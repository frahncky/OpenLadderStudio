$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'StudioUi.build.cs'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado. Execute PrepareUiAuditV51.ps1 antes.' }
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) { throw "Ancora nao encontrada em StudioUi.build.cs ($label)." }
    return $haystack.Replace($needle, $replacement)
}

# V68 deve rodar depois da auditoria V51, que ainda reaplica a antiga paleta escura.
$text = Replace-Required $text '        public static readonly Color Shell = Color.FromArgb(10, 31, 46);' '        public static readonly Color Shell = Color.FromArgb(247, 249, 252);' 'Shell'
$text = Replace-Required $text '        public static readonly Color Chrome = Color.FromArgb(14, 42, 61);' '        public static readonly Color Chrome = Color.FromArgb(255, 255, 255);' 'Chrome'
$text = Replace-Required $text '        public static readonly Color ChromeLight = Color.FromArgb(24, 58, 79);' '        public static readonly Color ChromeLight = Color.FromArgb(235, 243, 252);' 'ChromeLight'
$text = Replace-Required $text '        public static readonly Color Border = Color.FromArgb(48, 76, 94);' '        public static readonly Color Border = Color.FromArgb(207, 216, 226);' 'Border'
$text = Replace-Required $text '        public static readonly Color Accent = Color.FromArgb(47, 128, 237);' '        public static readonly Color Accent = Color.FromArgb(28, 112, 220);' 'Accent'
$text = Replace-Required $text '        public static readonly Color AccentDark = Color.FromArgb(35, 96, 178);' '        public static readonly Color AccentDark = Color.FromArgb(18, 70, 126);' 'AccentDark'
$text = Replace-Required $text '        public static readonly Color Workspace = Color.FromArgb(248, 250, 252);' '        public static readonly Color Workspace = Color.FromArgb(247, 249, 252);' 'Workspace'
$text = Replace-Required $text '        public static readonly Color Fore = Color.FromArgb(226, 230, 234);' '        public static readonly Color Fore = Color.FromArgb(31, 43, 56);' 'Fore'
$text = Replace-Required $text '        public static readonly Color Muted = Color.FromArgb(150, 157, 164);' '        public static readonly Color Muted = Color.FromArgb(93, 108, 124);' 'Muted'
$text = Replace-Required $text '        public static readonly Color NavBg = Color.FromArgb(14, 42, 61);' '        public static readonly Color NavBg = Color.FromArgb(247, 249, 252);' 'NavBg'
$text = Replace-Required $text '        public static readonly Color NavHover = Color.FromArgb(27, 63, 85);' '        public static readonly Color NavHover = Color.FromArgb(236, 244, 252);' 'NavHover'
$text = Replace-Required $text '        public static readonly Color NavActive = Color.FromArgb(25, 72, 105);' '        public static readonly Color NavActive = Color.FromArgb(224, 238, 252);' 'NavActive'
$text = Replace-Required $text '        public static readonly Color Faint = Color.FromArgb(108, 116, 124);' '        public static readonly Color Faint = Color.FromArgb(125, 138, 151);' 'Faint'
$text = Replace-Required $text '        public static readonly Color Disabled = Color.FromArgb(92, 97, 103);' '        public static readonly Color Disabled = Color.FromArgb(165, 173, 181);' 'Disabled'
$text = Replace-Required $text '            BackColor = Color.FromArgb(17, 23, 30);' '            BackColor = Color.FromArgb(255, 255, 255);' 'console'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'Studio UI V68 clara aplicada apos auditoria V51.'

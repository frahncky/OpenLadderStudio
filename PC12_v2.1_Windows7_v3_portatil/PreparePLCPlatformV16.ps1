$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'PLCPlatform.cs'
$outputPath = Join-Path (Get-Location) 'PLCPlatform.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$needle = @'
                PlcDeviceProfile profile = PlcDriverRegistry.FindProfile(id);
                return profile ?? PlcDriverRegistry.DefaultProfile;
'@
$replacement = @'
                PlcDeviceProfile profile = PlcDriverRegistry.FindProfile(id);
                if (profile == null) profile = CustomPlcProfileStore.Find(id);
                return profile ?? PlcDriverRegistry.DefaultProfile;
'@

if (-not $text.Contains($needle.Trim())) {
    throw 'Não foi possível localizar PlcProfileStore.Load em PLCPlatform.cs.'
}

$text = $text.Replace($needle.Trim(), $replacement.Trim())
[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)

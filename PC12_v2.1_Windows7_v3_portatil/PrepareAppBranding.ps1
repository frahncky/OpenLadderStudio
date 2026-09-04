$ErrorActionPreference = 'Stop'

function Add-BrandingInstall([string]$path) {
    if (-not (Test-Path $path)) { throw "Arquivo não encontrado: $path" }
    $text = [System.IO.File]::ReadAllText($path)
    $needle = '            Application.SetCompatibleTextRenderingDefault(false);'
    $replacement = "            Application.SetCompatibleTextRenderingDefault(false);`r`n            AppBranding.Install();"
    if (-not $text.Contains($needle)) { throw "Ponto de inicialização não encontrado em $path" }
    if (-not $text.Contains('AppBranding.Install();')) {
        $text = $text.Replace($needle, $replacement)
    }
    [System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
}

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Início não encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim não encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$root = Get-Location

$copies = @(
    @{ Source = 'PC12Updater.cs'; Target = 'PC12Updater.build.cs' },
    @{ Source = 'PLCDeviceManagerV16.cs'; Target = 'PLCDeviceManagerV16.build.cs' }
)

foreach ($item in $copies) {
    $source = Join-Path $root $item.Source
    $target = Join-Path $root $item.Target
    [System.IO.File]::WriteAllText($target, [System.IO.File]::ReadAllText($source), [System.Text.Encoding]::UTF8)
    Add-BrandingInstall $target
}

# Compatibilidade do atualizador com Windows 7.
# A API de releases continua sendo o caminho principal. Se o WebClient não conseguir
# consultar api.github.com, tenta o version.txt bruto e constrói URLs previsíveis da release.
# Se ambos falharem, oferece abertura da página de releases no navegador em vez de apenas
# mostrar "Falha de conexão" sem alternativa.
$updaterBuild = Join-Path $root 'PC12Updater.build.cs'
$updaterText = [System.IO.File]::ReadAllText($updaterBuild)

$repoConst = '        private const string RepoApi = "https://api.github.com/repos/frahncky/OpenLadderStudio/releases/latest";'
$repoReplacement = @'
        private const string RepoApi = "https://api.github.com/repos/frahncky/OpenLadderStudio/releases/latest";
        private const string RawVersionUrl = "https://raw.githubusercontent.com/frahncky/OpenLadderStudio/main/PC12_v2.1_Windows7_v3_portatil/version.txt";
        private const string ReleasesPage = "https://github.com/frahncky/OpenLadderStudio/releases/latest";
'@
if (-not $updaterText.Contains($repoConst)) { throw 'Constante RepoApi não encontrada no atualizador.' }
$updaterText = $updaterText.Replace($repoConst, $repoReplacement.TrimEnd())

$oldClick = '            updateButton.Click += delegate { DownloadAndInstall(); };'
$newClick = @'
            updateButton.Click += delegate
            {
                if (string.IsNullOrEmpty(setupUrl)) OpenReleasesPage();
                else DownloadAndInstall();
            };
'@
if (-not $updaterText.Contains($oldClick)) { throw 'Handler do botão ATUALIZAR não encontrado.' }
$updaterText = $updaterText.Replace($oldClick, $newClick.TrimEnd())

$checkReplacement = @'
        private void CheckForUpdates()
        {
            checkButton.Enabled = false;
            updateButton.Enabled = false;
            updateButton.Text = "ATUALIZAR";
            progress.Value = 0;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Verificando...";
            Application.DoEvents();

            latestVersion = string.Empty;
            setupUrl = string.Empty;
            hashUrl = string.Empty;
            string primaryError = string.Empty;

            try
            {
                EnableTls12();
                using (WebClient wc = NewClient())
                {
                    string json = wc.DownloadString(RepoApi);
                    latestVersion = Extract(json, "\\\"tag_name\\\"\\s*:\\s*\\\"v?([^\\\"]+)\\\"");
                    setupUrl = ExtractAssetUrl(json, SetupAssetName);
                    hashUrl = ExtractAssetUrl(json, HashAssetName);
                }
            }
            catch (Exception ex)
            {
                primaryError = ex.Message;
            }

            if (string.IsNullOrEmpty(latestVersion))
            {
                try
                {
                    EnableTls12();
                    using (WebClient wc = NewClient())
                    {
                        latestVersion = (wc.DownloadString(RawVersionUrl) ?? string.Empty).Trim().TrimStart('v', 'V');
                    }
                    if (!Regex.IsMatch(latestVersion, "^\\d+(?:\\.\\d+){1,2}$"))
                        throw new InvalidDataException("Versão remota inválida.");

                    setupUrl = "https://github.com/frahncky/OpenLadderStudio/releases/download/v" + latestVersion + "/" + SetupAssetName;
                    hashUrl = "https://github.com/frahncky/OpenLadderStudio/releases/download/v" + latestVersion + "/" + HashAssetName;
                    statusLabel.Text = "Conectado em modo de compatibilidade.";
                }
                catch (Exception fallbackEx)
                {
                    latestVersion = string.Empty;
                    setupUrl = string.Empty;
                    hashUrl = string.Empty;
                    availableLabel.Text = "—";
                    statusLabel.ForeColor = Warning;
                    statusLabel.Text = "GitHub inacessível pelo atualizador. Abra a release no navegador.";
                    updateButton.Text = "ABRIR RELEASE";
                    updateButton.Enabled = true;
                    if (!string.IsNullOrEmpty(primaryError))
                        statusLabel.Tag = primaryError + " | " + fallbackEx.Message;
                    return;
                }
            }

            try
            {
                if (string.IsNullOrEmpty(latestVersion)) throw new InvalidDataException("Versão inválida.");
                availableLabel.Text = "v" + latestVersion;

                if (CompareVersions(latestVersion, currentVersion) <= 0)
                {
                    statusLabel.ForeColor = Success;
                    statusLabel.Text = "Versão atualizada.";
                    return;
                }

                if (string.IsNullOrEmpty(setupUrl) || string.IsNullOrEmpty(hashUrl))
                {
                    statusLabel.ForeColor = Warning;
                    statusLabel.Text = "Pacote de atualização incompleto.";
                    return;
                }

                updateButton.Text = "ATUALIZAR";
                updateButton.Enabled = true;
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Nova versão disponível.";
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Falha: " + ex.Message;
            }
            finally
            {
                checkButton.Enabled = true;
            }
        }

        private void OpenReleasesPage()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ReleasesPage;
                psi.UseShellExecute = true;
                Process.Start(psi);
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Página de releases aberta no navegador.";
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Não foi possível abrir o navegador: " + ex.Message;
            }
        }

'@
$updaterText = Replace-Section $updaterText '        private void CheckForUpdates()' '        private void DownloadAndInstall()' $checkReplacement 'CheckForUpdates'

$oldTls = @'
        private static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        }
'@
$newTls = @'
        private static void EnableTls12()
        {
            try
            {
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            }
            catch { }
        }
'@
if (-not $updaterText.Contains($oldTls.Trim())) { throw 'Método EnableTls12 não encontrado.' }
$updaterText = $updaterText.Replace($oldTls.Trim(), $newTls.Trim())

$oldClient = @'
        private static WebClient NewClient()
        {
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "OpenLadder-Studio-Updater";
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }
'@
$newClient = @'
        private static WebClient NewClient()
        {
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "OpenLadder-Studio-Updater";
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            try
            {
                wc.Proxy = WebRequest.DefaultWebProxy;
                if (wc.Proxy != null) wc.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }
            catch { }
            return wc;
        }
'@
if (-not $updaterText.Contains($oldClient.Trim())) { throw 'Método NewClient não encontrado.' }
$updaterText = $updaterText.Replace($oldClient.Trim(), $newClient.Trim())

# O instalador controla explicitamente se deve reabrir o OpenLadder Studio.
# Impede que o Restart Manager e a etapa [Run] tentem iniciar duas instâncias.
$oldArgs = '/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS'
$newArgs = '/SILENT /CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS'
if (-not $updaterText.Contains($oldArgs)) {
    throw 'Argumentos de instalação não encontrados em PC12Updater.build.cs.'
}
$updaterText = $updaterText.Replace($oldArgs, $newArgs)
[System.IO.File]::WriteAllText($updaterBuild, $updaterText, [System.Text.Encoding]::UTF8)

$generated = @(
    'LadderEditor.build.cs',
    'PLCMemoryMapManagerV15.build.cs',
    'ModbusMonitorV18.build.cs',
    'UniversalStudioShell.build.cs'
)

foreach ($name in $generated) {
    Add-BrandingInstall (Join-Path $root $name)
}

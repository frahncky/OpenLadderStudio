$ErrorActionPreference = 'Stop'
$root = Get-Location

function Replace-Once([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada: $label" }
    return $text.Replace($needle, $replacement)
}

$updaterPath = Join-Path $root 'PC12Updater.build.cs'
$updater = [System.IO.File]::ReadAllText($updaterPath)
$updaterAnchor = "namespace ModernPC12`r`n{"
$updaterCode = @'
namespace ModernPC12
{
    internal static class PC12UpdateChecker
    {
        private const string RepoApi = "https://api.github.com/repos/frahncky/OpenLadderStudio/releases/latest";

        public static bool TryGetAvailableVersion(out string latestVersion)
        {
            latestVersion = string.Empty;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "OpenLadder-Studio-Updater";
                    string json = client.DownloadString(RepoApi);
                    Match match = Regex.Match(json ?? string.Empty, "\\\"tag_name\\\"\\s*:\\s*\\\"v?([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                    if (!match.Success) return false;
                    latestVersion = match.Groups[1].Value.Trim();
                    Version latest;
                    Version current;
                    return Version.TryParse(latestVersion, out latest)
                        && Version.TryParse(ReadCurrentVersion(), out current)
                        && latest.CompareTo(current) > 0;
                }
            }
            catch { latestVersion = string.Empty; return false; }
        }

        private static string ReadCurrentVersion()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim().TrimStart('v', 'V');
            }
            catch { }
            return "0.10";
        }
    }
'@
$updater = Replace-Once $updater $updaterAnchor ($updaterCode.TrimEnd()) 'namespace do atualizador'
[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)

$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$shell = [System.IO.File]::ReadAllText($shellPath)
$shell = Replace-Once $shell 'using System.Reflection;' "using System.Reflection;`r`nusing System.Threading;" 'using Thread'
$shell = Replace-Once $shell '        private Label statusText;' "        private Label statusText;`r`n        private LinkLabel updateNotice;" 'campo do aviso'
$shell = Replace-Once $shell '            ShowLadder();' "            ShowLadder();`r`n            Shown += delegate { CheckForUpdatesInBackground(); };" 'inicio da verificacao'
$statusAnchor = '            p.Controls.Add(statusText);'
$statusCode = @'
            p.Controls.Add(statusText);

            updateNotice = new LinkLabel();
            updateNotice.Dock = DockStyle.Right;
            updateNotice.Width = 230;
            updateNotice.TextAlign = ContentAlignment.MiddleRight;
            updateNotice.LinkColor = Color.FromArgb(69, 190, 129);
            updateNotice.ActiveLinkColor = Color.White;
            updateNotice.VisitedLinkColor = Color.FromArgb(69, 190, 129);
            updateNotice.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            updateNotice.Visible = false;
            updateNotice.Cursor = Cursors.Hand;
            updateNotice.Click += delegate { ShowUpdater(); };
            p.Controls.Add(updateNotice);
'@
$shell = Replace-Once $shell $statusAnchor ($statusCode.TrimEnd()) 'controle visual do aviso'
$methodAnchor = '        private void RefreshProfileUi()'
$methodCode = @'
        private void CheckForUpdatesInBackground()
        {
            Thread worker = new Thread(delegate()
            {
                string latestVersion;
                if (!PC12UpdateChecker.TryGetAvailableVersion(out latestVersion) || IsDisposed) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || updateNotice == null) return;
                        updateNotice.Text = "Atualização v" + latestVersion + " disponível";
                        updateNotice.Visible = true;
                    });
                }
                catch (InvalidOperationException) { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void RefreshProfileUi()
'@
$shell = Replace-Once $shell $methodAnchor ($methodCode.TrimEnd()) 'metodo de verificacao'
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

$ErrorActionPreference = 'Stop'
$root = Get-Location

function Invoke-ReplaceText([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada: $label" }
    return $text.Replace($needle, $replacement)
}

$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$shell = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")
$shell = Invoke-ReplaceText $shell 'using System.Drawing;' "using System.Drawing;`nusing System.IO;`nusing System.Net;" 'imports de rede'
$shell = Invoke-ReplaceText $shell 'using System.Reflection;' "using System.Reflection;`nusing System.Text.RegularExpressions;`nusing System.Threading;" 'imports de verificacao'

$checker = @'
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
$shell = Invoke-ReplaceText $shell "namespace ModernPC12`n{" $checker.TrimEnd() 'namespace do shell'
$shell = Invoke-ReplaceText $shell '        private Label statusText;' "        private Label statusText;`n        private LinkLabel updateNotice;" 'campo do aviso'
$shell = Invoke-ReplaceText $shell '            ShowLadder();' "            ShowLadder();`n            Shown += delegate { CheckForUpdatesInBackground(); };" 'inicio da verificacao'

$notice = @'
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
$shell = Invoke-ReplaceText $shell '            p.Controls.Add(statusText);' $notice.TrimEnd() 'controle visual do aviso'

$method = @'
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
$shell = Invoke-ReplaceText $shell '        private void RefreshProfileUi()' $method.TrimEnd() 'metodo de verificacao'
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

& (Join-Path $root 'PrepareUpdateResumeV50.ps1')
& (Join-Path $root 'PrepareUiAuditV52.ps1')
& (Join-Path $root 'PrepareUiFixV53.ps1')
& (Join-Path $root 'PrepareUiPolishV54.ps1')
& (Join-Path $root 'PrepareUiConsistencyV55.ps1')

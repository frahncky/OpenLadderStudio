$ErrorActionPreference = 'Stop'

$updaterPath = Join-Path (Get-Location) 'PC12Updater.build.cs'
$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'

if (-not (Test-Path $updaterPath)) { throw 'PC12Updater.build.cs não encontrado.' }
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs não encontrado.' }

$updater = [System.IO.File]::ReadAllText($updaterPath)

$mainNeedle = '        private static void Main()'
$mainReplacement = '        private static void Main(string[] args)'
if (-not $updater.Contains($mainNeedle)) { throw 'Main do atualizador não encontrado.' }
$updater = $updater.Replace($mainNeedle, $mainReplacement)

$runNeedle = '            Application.Run(new PC12UpdaterForm());'
$runReplacement = @'
            bool automatic = args != null && args.Length > 0
                && string.Equals(args[0], "/AUTO", StringComparison.OrdinalIgnoreCase);
            Application.Run(new PC12UpdaterForm(automatic));
'@
if (-not $updater.Contains($runNeedle)) { throw 'Application.Run do atualizador não encontrado.' }
$updater = $updater.Replace($runNeedle, $runReplacement.TrimEnd())

$ctorNeedle = '        public PC12UpdaterForm()'
$ctorReplacement = @'
        public PC12UpdaterForm() : this(false)
        {
        }

        public PC12UpdaterForm(bool automatic)
'@
if (-not $updater.Contains($ctorNeedle)) { throw 'Construtor do atualizador não encontrado.' }
$updater = $updater.Replace($ctorNeedle, $ctorReplacement.TrimEnd())

$buildNeedle = @'
            currentVersion = ReadCurrentVersion();
            BuildUi();
        }

        private void BuildUi()
'@
$buildReplacement = @'
            currentVersion = ReadCurrentVersion();
            BuildUi();

            if (automatic)
            {
                ShowInTaskbar = false;
                Opacity = 0.0;
                WindowState = FormWindowState.Minimized;
                Shown += delegate
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        try
                        {
                            CheckForUpdates();
                            if (updateButton.Enabled
                                && string.Equals(updateButton.Text, "ATUALIZAR", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrEmpty(setupUrl))
                            {
                                DownloadAndInstall();
                            }
                            else
                            {
                                Close();
                            }
                        }
                        catch
                        {
                            Close();
                        }
                    }));
                };
            }
        }

        private void BuildUi()
'@
if (-not $updater.Contains($buildNeedle.Trim())) { throw 'Final do construtor do atualizador não encontrado.' }
$updater = $updater.Replace($buildNeedle.Trim(), $buildReplacement.Trim())

[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)

$shell = [System.IO.File]::ReadAllText($shellPath)
if (-not $shell.Contains('using System.Diagnostics;')) {
    $shell = $shell.Replace('using System.Drawing;', "using System.Drawing;`r`nusing System.Diagnostics;`r`nusing System.IO;")
}

$shownNeedle = @'
            BuildUi();
            RefreshProfileUi();
            ShowLadder();
        }
'@
$shownReplacement = @'
            BuildUi();
            RefreshProfileUi();
            ShowLadder();
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { StartAutomaticUpdater(); })); };
        }
'@
if (-not $shell.Contains($shownNeedle.Trim())) { throw 'Construtor do shell não encontrado.' }
$shell = $shell.Replace($shownNeedle.Trim(), $shownReplacement.Trim())

$methodAnchor = '        private void BuildUi()'
$methodInsert = @'
        private void StartAutomaticUpdater()
        {
            try
            {
                string updater = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenLadderUpdater.exe");
                if (!File.Exists(updater)) return;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = updater;
                psi.Arguments = "/AUTO";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
                // Atualização automática nunca deve impedir a abertura do Studio.
            }
        }

'@
if (-not $shell.Contains($methodAnchor)) { throw 'BuildUi do shell não encontrado.' }
$shell = $shell.Replace($methodAnchor, $methodInsert + $methodAnchor)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

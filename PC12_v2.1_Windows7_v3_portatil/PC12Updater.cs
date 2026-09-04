using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class PC12UpdaterProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PC12UpdaterForm());
        }
    }

    internal sealed class PC12UpdaterForm : Form
    {
        private const string RepoApi = "https://api.github.com/repos/frahncky/OpenLadderStudio/releases/latest";
        private const string SetupAssetName = "OpenLadder-Studio-Setup.exe";
        private const string HashAssetName = "OpenLadder-Studio-Setup.exe.sha256";

        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private Label currentLabel;
        private Label availableLabel;
        private Label statusLabel;
        private ProgressBar progress;
        private Button checkButton;
        private Button updateButton;
        private string currentVersion;
        private string latestVersion;
        private string setupUrl;
        private string hashUrl;
        private string downloadedSetup;

        public PC12UpdaterForm()
        {
            Text = "OpenLadder Studio - Atualização";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(620, 300);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            currentVersion = ReadCurrentVersion();
            BuildUi();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 56;
            header.BackColor = Color.White;
            Controls.Add(header);
            header.Controls.Add(NewLabel("OPENLADDER STUDIO", 14.0f, FontStyle.Bold, Navy, 20, 16));

            Panel card = new Panel();
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.FixedSingle;
            card.Location = new Point(20, 76);
            card.Size = new Size(580, 190);
            Controls.Add(card);

            card.Controls.Add(NewLabel("Instalada", 8.2f, FontStyle.Bold, TextSecondary, 20, 18));
            currentLabel = NewLabel("v" + currentVersion, 16.0f, FontStyle.Bold, TextPrimary, 20, 40);
            card.Controls.Add(currentLabel);

            card.Controls.Add(NewLabel("Disponível", 8.2f, FontStyle.Bold, TextSecondary, 180, 18));
            availableLabel = NewLabel("—", 16.0f, FontStyle.Bold, TextPrimary, 180, 40);
            card.Controls.Add(availableLabel);

            checkButton = ButtonAt("VERIFICAR", 20, 84, 130, true);
            checkButton.Click += delegate { CheckForUpdates(); };
            card.Controls.Add(checkButton);

            updateButton = ButtonAt("ATUALIZAR", 162, 84, 130, false);
            updateButton.Enabled = false;
            updateButton.Click += delegate { DownloadAndInstall(); };
            card.Controls.Add(updateButton);

            progress = new ProgressBar();
            progress.Location = new Point(20, 132);
            progress.Size = new Size(538, 15);
            card.Controls.Add(progress);

            statusLabel = NewLabel("", 8.5f, FontStyle.Regular, TextSecondary, 20, 157);
            statusLabel.MaximumSize = new Size(538, 30);
            card.Controls.Add(statusLabel);
        }

        private void CheckForUpdates()
        {
            checkButton.Enabled = false;
            updateButton.Enabled = false;
            progress.Value = 0;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Verificando...";
            Application.DoEvents();

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

                updateButton.Enabled = true;
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Nova versão disponível.";
            }
            catch (WebException ex)
            {
                statusLabel.ForeColor = Warning;
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                statusLabel.Text = resp != null && resp.StatusCode == HttpStatusCode.NotFound ? "Nenhuma release publicada." : "Falha de conexão.";
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

        private void DownloadAndInstall()
        {
            if (string.IsNullOrEmpty(setupUrl) || string.IsNullOrEmpty(hashUrl)) return;
            updateButton.Enabled = false;
            checkButton.Enabled = false;
            progress.Value = 0;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Baixando...";

            string tempDir = Path.Combine(Path.GetTempPath(), "OpenLadderStudioUpdate");
            Directory.CreateDirectory(tempDir);
            downloadedSetup = Path.Combine(tempDir, SetupAssetName);
            string hashFile = Path.Combine(tempDir, HashAssetName);

            try
            {
                EnableTls12();
                using (WebClient wc = NewClient())
                {
                    wc.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
                    {
                        int value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                        progress.Value = value;
                        statusLabel.Text = "Baixando... " + value.ToString(CultureInfo.InvariantCulture) + "%";
                    };
                    wc.DownloadFileCompleted += delegate(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
                    {
                        if (e.Cancelled || e.Error != null)
                        {
                            statusLabel.ForeColor = Warning;
                            statusLabel.Text = "Falha no download.";
                            checkButton.Enabled = true;
                            return;
                        }
                        VerifyAndLaunch(hashFile);
                    };
                    using (WebClient hashClient = NewClient()) hashClient.DownloadFile(hashUrl, hashFile);
                    wc.DownloadFileAsync(new Uri(setupUrl), downloadedSetup);
                }
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Falha: " + ex.Message;
                checkButton.Enabled = true;
            }
        }

        private void VerifyAndLaunch(string hashFile)
        {
            try
            {
                statusLabel.Text = "Verificando...";
                Match m = Regex.Match(File.ReadAllText(hashFile).Trim(), "([0-9A-Fa-f]{64})");
                if (!m.Success) throw new InvalidDataException("SHA-256 inválido.");
                if (!string.Equals(m.Groups[1].Value, Sha256(downloadedSetup), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 não confere.");

                progress.Value = 100;
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = downloadedSetup;
                psi.Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";
                psi.UseShellExecute = true;
                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Atualização bloqueada: " + ex.Message;
                checkButton.Enabled = true;
            }
        }

        private string ReadCurrentVersion()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim().TrimStart('v', 'V');
            }
            catch { }
            return "0.10";
        }

        private static WebClient NewClient()
        {
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "OpenLadder-Studio-Updater";
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        private static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        }

        private static string Extract(string text, string pattern)
        {
            Match m = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        private static string ExtractAssetUrl(string json, string assetName)
        {
            string escaped = Regex.Escape(assetName);
            string pattern = "\\\"name\\\"\\s*:\\s*\\\"" + escaped + "\\\"[\\s\\S]*?\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"";
            string found = Extract(json, pattern);
            if (!string.IsNullOrEmpty(found)) return found.Replace("\\/", "/");
            pattern = "\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]*/" + escaped + ")\\\"";
            return Extract(json, pattern).Replace("\\/", "/");
        }

        private static int CompareVersions(string a, string b)
        {
            string[] pa = (a ?? "").Trim().TrimStart('v', 'V').Split('.');
            string[] pb = (b ?? "").Trim().TrimStart('v', 'V').Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int va = i < pa.Length ? ParseLeadingInt(pa[i]) : 0;
                int vb = i < pb.Length ? ParseLeadingInt(pb[i]) : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        private static int ParseLeadingInt(string s)
        {
            Match m = Regex.Match(s ?? "", "^\\d+");
            int value;
            return m.Success && int.TryParse(m.Value, out value) ? value : 0;
        }

        private static string Sha256(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                return sb.ToString();
            }
        }

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Navy;
                b.FlatAppearance.BorderColor = Color.FromArgb(195, 207, 220);
            }
            return b;
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color, int left, int top)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.Location = new Point(left, top);
            return l;
        }
    }
}

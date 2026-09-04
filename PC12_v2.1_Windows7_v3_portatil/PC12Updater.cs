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
        private const string RepoApi = "https://api.github.com/repos/frahncky/UpgradeInterfacePLC/releases/latest";
        private const string SetupAssetName = "PC12-Studio-TP02-Setup.exe";
        private const string HashAssetName = "PC12-Studio-TP02-Setup.exe.sha256";

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
            Text = "PC12 Studio Updater";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(660, 390);
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
            header.Height = 78;
            header.BackColor = Color.White;
            Controls.Add(header);

            header.Controls.Add(NewLabel("PC12 STUDIO UPDATER", 15.0f, FontStyle.Bold, Navy, 22, 14));
            header.Controls.Add(NewLabel("Atualizações oficiais publicadas em GitHub Releases", 8.7f, FontStyle.Regular, TextSecondary, 24, 46));

            Panel card = new Panel();
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.FixedSingle;
            card.Location = new Point(22, 100);
            card.Size = new Size(616, 240);
            Controls.Add(card);

            card.Controls.Add(NewLabel("Versão instalada", 8.3f, FontStyle.Bold, TextSecondary, 22, 20));
            currentLabel = NewLabel("v" + currentVersion, 17.0f, FontStyle.Bold, TextPrimary, 22, 43);
            card.Controls.Add(currentLabel);

            card.Controls.Add(NewLabel("Versão disponível", 8.3f, FontStyle.Bold, TextSecondary, 210, 20));
            availableLabel = NewLabel("—", 17.0f, FontStyle.Bold, TextPrimary, 210, 43);
            card.Controls.Add(availableLabel);

            checkButton = ButtonAt("VERIFICAR ATUALIZAÇÕES", 22, 92, 190, true);
            checkButton.Click += delegate { CheckForUpdates(); };
            card.Controls.Add(checkButton);

            updateButton = ButtonAt("ATUALIZAR AGORA", 224, 92, 160, false);
            updateButton.Enabled = false;
            updateButton.Click += delegate { DownloadAndInstall(); };
            card.Controls.Add(updateButton);

            progress = new ProgressBar();
            progress.Location = new Point(22, 145);
            progress.Size = new Size(570, 18);
            progress.Style = ProgressBarStyle.Continuous;
            card.Controls.Add(progress);

            statusLabel = NewLabel("Clique em Verificar atualizações. Nenhuma alteração é feita sem sua autorização.", 8.7f, FontStyle.Regular, TextSecondary, 22, 178);
            statusLabel.MaximumSize = new Size(570, 45);
            card.Controls.Add(statusLabel);

            Label note = NewLabel("Segurança: o instalador só é aceito se a Release também publicar o SHA-256 correspondente.", 8.2f, FontStyle.Bold, Success, 24, 352);
            note.MaximumSize = new Size(610, 0);
            Controls.Add(note);
        }

        private void CheckForUpdates()
        {
            checkButton.Enabled = false;
            updateButton.Enabled = false;
            progress.Value = 0;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Consultando a versão mais recente...";
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

                if (string.IsNullOrEmpty(latestVersion)) throw new InvalidDataException("A Release não informa tag de versão.");
                availableLabel.Text = "v" + latestVersion;

                int cmp = CompareVersions(latestVersion, currentVersion);
                if (cmp <= 0)
                {
                    statusLabel.ForeColor = Success;
                    statusLabel.Text = "Você já está usando a versão mais recente.";
                    return;
                }

                if (string.IsNullOrEmpty(setupUrl) || string.IsNullOrEmpty(hashUrl))
                {
                    statusLabel.ForeColor = Warning;
                    statusLabel.Text = "Existe uma versão nova, mas a Release não possui o Setup.exe e o SHA-256 exigidos. A atualização automática foi bloqueada por segurança.";
                    return;
                }

                updateButton.Enabled = true;
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Nova versão disponível. Clique em Atualizar agora para baixar e instalar.";
            }
            catch (WebException ex)
            {
                statusLabel.ForeColor = Warning;
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound)
                    statusLabel.Text = "Ainda não existe uma Release pública do PC12 Studio. A instalação atual continua funcionando normalmente.";
                else
                    statusLabel.Text = "Não foi possível consultar as atualizações. Verifique a conexão com a Internet.";
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Falha ao verificar atualizações: " + ex.Message;
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
            statusLabel.Text = "Baixando o novo instalador...";

            string tempDir = Path.Combine(Path.GetTempPath(), "PC12StudioUpdate");
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
                        int value = e.ProgressPercentage;
                        if (value < 0) value = 0;
                        if (value > 100) value = 100;
                        progress.Value = value;
                        statusLabel.Text = "Baixando atualização... " + value.ToString(CultureInfo.InvariantCulture) + "%";
                    };
                    wc.DownloadFileCompleted += delegate(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
                    {
                        if (e.Cancelled || e.Error != null)
                        {
                            statusLabel.ForeColor = Warning;
                            statusLabel.Text = "Falha no download da atualização.";
                            checkButton.Enabled = true;
                            return;
                        }
                        VerifyAndLaunch(hashFile);
                    };

                    using (WebClient hashClient = NewClient()) hashClient.DownloadFile(hashUrl, hashFile);
                    wc.DownloadFileAsync(new Uri(setupUrl), downloadedSetup);
                    return;
                }
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Warning;
                statusLabel.Text = "Não foi possível baixar a atualização: " + ex.Message;
                checkButton.Enabled = true;
            }
        }

        private void VerifyAndLaunch(string hashFile)
        {
            try
            {
                statusLabel.Text = "Verificando SHA-256...";
                string expectedText = File.ReadAllText(hashFile).Trim();
                Match m = Regex.Match(expectedText, "([0-9A-Fa-f]{64})");
                if (!m.Success) throw new InvalidDataException("Arquivo SHA-256 inválido.");
                string expected = m.Groups[1].Value.ToUpperInvariant();
                string actual = Sha256(downloadedSetup);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O SHA-256 do instalador não confere. A instalação foi cancelada.");

                progress.Value = 100;
                statusLabel.ForeColor = Success;
                statusLabel.Text = "Integridade confirmada. Abrindo o instalador...";
                Application.DoEvents();

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
            return "0.7";
        }

        private static WebClient NewClient()
        {
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "PC12-Studio-Updater";
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
            found = Extract(json, pattern);
            return found.Replace("\\/", "/");
        }

        private static int CompareVersions(string a, string b)
        {
            string[] pa = (a ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
            string[] pb = (b ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            int i;
            for (i = 0; i < n; i++)
            {
                int va = i < pa.Length ? ParseLeadingInt(pa[i]) : 0;
                int vb = i < pb.Length ? ParseLeadingInt(pb[i]) : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        private static int ParseLeadingInt(string s)
        {
            Match m = Regex.Match(s ?? string.Empty, "^\\d+");
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
                int i;
                for (i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                return sb.ToString();
            }
        }

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 38);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
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

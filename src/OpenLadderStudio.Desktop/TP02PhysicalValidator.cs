using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    internal static class TP02PhysicalValidatorProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBranding.Install();
            Application.Run(new TP02PhysicalValidatorForm());
        }
    }

    internal static class TP02PhysicalValidationSafety
    {
        internal static readonly byte[] Hello = new byte[] { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
        internal static readonly byte[] F0 = new byte[] { 0xF0, 0x00, 0x0F };
        internal static readonly byte[] Frame38 = new byte[] { 0x38, 0x00, 0xC7 };

        internal static bool IsAllowed(byte[] frame)
        {
            if (Same(frame, Hello) || Same(frame, F0) || Same(frame, Frame38)) return true;
            if (frame == null || frame.Length < 3 || !Tp02PgProtocol.HasValidChecksum(frame)) return false;
            if (frame[0] == 0x34)
            {
                if (frame.Length != 6 || frame[1] != 0x03 || frame[4] != 0xA0) return false;
                int start = (frame[2] << 8) | frame[3];
                return start >= 0 && start < Tp02Pg34Pager.MaxProgramSteps;
            }
            if (frame[0] == 0x0A)
                return frame.Length == 6 && frame[1] == 0x03 && frame[4] != 0;
            return false;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    internal sealed class TP02PhysicalValidatorForm : Form
    {
        private sealed class CheckResult
        {
            internal string Name;
            internal bool Pass;
            internal string Detail;
        }

        private readonly ComboBox ports = new ComboBox();
        private readonly Button run = new Button();
        private readonly Button refresh = new Button();
        private readonly Button folder = new Button();
        private readonly TextBox log = new TextBox();
        private readonly Label status = new Label();
        private string sessionDir = string.Empty;
        private string sessionLog = string.Empty;
        private bool busy;

        internal TP02PhysicalValidatorForm()
        {
            Text = "OpenLadder - Validação física TP02 READ-ONLY";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(1060, 720);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.FromArgb(18, 24, 31);
            ForeColor = Color.FromArgb(226, 230, 234);
            BuildUi();
            RefreshPorts();
        }

        private void BuildUi()
        {
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 150;
            top.BackColor = Color.FromArgb(27, 36, 46);
            Controls.Add(top);

            Label title = new Label();
            title.Text = "TP02 - VALIDAÇÃO FÍSICA READ-ONLY";
            title.Font = new Font("Segoe UI", 14.0f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(18, 14);
            top.Controls.Add(title);

            Label info = new Label();
            info.Text = "Allowlist rígida: CON-ICB, F0, 38, PG34 e PG0A. Não transmite 09, 33, 35, Clear, RUN, STOP ou EEPROM.";
            info.AutoSize = true;
            info.MaximumSize = new Size(980, 0);
            info.ForeColor = Color.FromArgb(224, 170, 64);
            info.Location = new Point(20, 45);
            top.Controls.Add(info);

            ports.DropDownStyle = ComboBoxStyle.DropDownList;
            ports.Location = new Point(20, 89);
            ports.Width = 135;
            top.Controls.Add(ports);

            refresh.Text = "Atualizar COM";
            refresh.Location = new Point(165, 87);
            refresh.Size = new Size(115, 28);
            refresh.Click += delegate { RefreshPorts(); };
            top.Controls.Add(refresh);

            run.Text = "Executar campanha READ-ONLY";
            run.Location = new Point(300, 82);
            run.Size = new Size(230, 38);
            run.BackColor = Color.FromArgb(38, 166, 154);
            run.ForeColor = Color.White;
            run.FlatStyle = FlatStyle.Flat;
            run.FlatAppearance.BorderSize = 0;
            run.Click += delegate { StartCampaign(); };
            top.Controls.Add(run);

            folder.Text = "Abrir pasta";
            folder.Location = new Point(545, 87);
            folder.Size = new Size(110, 28);
            folder.Enabled = false;
            folder.Click += delegate { OpenFolder(); };
            top.Controls.Add(folder);

            status.Text = "AGUARDANDO";
            status.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            status.AutoSize = true;
            status.Location = new Point(680, 93);
            status.ForeColor = Color.FromArgb(158, 169, 180);
            top.Controls.Add(status);

            log.Multiline = true;
            log.ReadOnly = true;
            log.WordWrap = false;
            log.ScrollBars = ScrollBars.Both;
            log.Dock = DockStyle.Fill;
            log.Font = new Font("Consolas", 9.0f);
            log.BackColor = Color.FromArgb(16, 22, 29);
            log.ForeColor = Color.FromArgb(226, 230, 234);
            Controls.Add(log);
            log.BringToFront();
        }

        private void RefreshPorts()
        {
            string selected = ports.SelectedItem == null ? string.Empty : ports.SelectedItem.ToString();
            string[] values = SerialPort.GetPortNames();
            Array.Sort(values, StringComparer.OrdinalIgnoreCase);
            ports.Items.Clear();
            foreach (string value in values) ports.Items.Add(value);
            if (!string.IsNullOrEmpty(selected) && ports.Items.Contains(selected)) ports.SelectedItem = selected;
            else if (ports.Items.Count > 0) ports.SelectedIndex = 0;
        }

        private void StartCampaign()
        {
            if (busy) return;
            if (ports.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM do TP-232PG/conversor.", "TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string portName = ports.SelectedItem.ToString();
            CreateSession();
            log.Clear();
            Append("Campanha física READ-ONLY v1.48.");
            Append("Porta: " + portName + " | 19200 8O1 | DTR=OFF | RTS=OFF.");
            Append("Bloqueados por código: 09, 33, 35, Clear, RUN, STOP e EEPROM.");
            SetBusy(true);
            SetStatus("EXECUTANDO", Color.FromArgb(224, 170, 64));

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                List<CheckResult> results = null;
                try { results = RunCampaign(portName); }
                catch (Exception ex) { failure = ex; }
                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        Append("FALHA FINAL: " + failure.Message);
                        SetStatus("FALHA", Color.FromArgb(214, 87, 87));
                    }
                    else
                    {
                        bool all = true;
                        foreach (CheckResult r in results) if (!r.Pass) all = false;
                        SetStatus(all ? "CAMPANHA APROVADA" : "CAMPANHA PARCIAL", all ? Color.FromArgb(74, 190, 119) : Color.FromArgb(224, 170, 64));
                        MessageBox.Show(this,
                            "Campanha concluída.\r\n\r\nOs arquivos foram salvos em:\r\n" + sessionDir
                            + "\r\n\r\nEnvie a pasta/arquivos de captura para fechar a evidência física do protocolo.",
                            "TP02 - validação física", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private List<CheckResult> RunCampaign(string portName)
        {
            List<CheckResult> results = new List<CheckResult>();
            SerialPort port = null;
            try
            {
                port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.DtrEnable = false;
                port.RtsEnable = false;
                port.ReadTimeout = 100;
                port.WriteTimeout = 1500;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                Thread.Sleep(1400);

                string hello = ValidateHello(port);
                Add(results, "HELLO", true, hello);

                byte[] f0raw = ExchangeRaw(port, TP02PhysicalValidationSafety.F0, 3600, 260, "F0");
                byte[] f0expected = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };
                bool f0ok = Contains(f0raw, f0expected);
                Add(results, "F0", f0ok, f0ok ? ToHex(f0expected) : "Resposta esperada não localizada");

                byte[] r38raw = ExchangeRaw(port, TP02PhysicalValidationSafety.Frame38, 3600, 280, "38");
                byte[] r38 = FindFrame(r38raw, 2);
                Add(results, "38", r38 != null, r38 == null ? "Quadro LEN=02 não localizado" : ToHex(r38));

                ValidatePg34(port, results, 0, "PG34-P0");
                ValidatePg34(port, results, 80, "PG34-P80");

                ValidateRead(port, results, "0A-V001", Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.V, 1, 2), 2, true);
                ValidateRead(port, results, "0A-D001", Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.D, 1, 2), 2, true);
                ValidateRead(port, results, "0A-WC001", Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.WC, 1, 2), 2, true);
                ValidateRead(port, results, "0A-FL001", Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.FL, 1, 20), 20, false);
                ValidateClock(port, results);
                ValidateScan(port, results);

                WriteSummary(results, hello);
                return results;
            }
            finally
            {
                if (port != null)
                {
                    try { if (port.IsOpen) port.Close(); } catch { }
                    try { port.Dispose(); } catch { }
                }
            }
        }

        private string ValidateHello(SerialPort port)
        {
            byte[] stop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
            byte[] runState = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
            for (int i = 1; i <= 6; i++)
            {
                byte[] raw = ExchangeRaw(port, TP02PhysicalValidationSafety.Hello, i == 1 ? 2600 : 3000, 240, "HELLO-" + i.ToString(CultureInfo.InvariantCulture));
                if (Contains(raw, stop)) return "80 01 09 75 (STOP)";
                if (Contains(raw, runState)) return "C0 01 09 35 (RUN)";
                Thread.Sleep(350);
            }
            throw new TimeoutException("HELLO não confirmado em 6 tentativas.");
        }

        private void ValidatePg34(SerialPort port, List<CheckResult> results, int start, string label)
        {
            byte[] request = Tp02Pg34Pager.BuildReadRequest(start);
            byte[] raw = ExchangeRaw(port, request, 5500, 300, label);
            byte[] frame = FindFrame(raw, Tp02Pg34Pager.PayloadLength);
            if (frame == null)
            {
                Add(results, label, false, "Resposta LEN=F0/checksum FF não localizada");
                return;
            }
            int localEnd;
            bool hasEnd = Tp02Pg34Pager.TryFindEnd(frame, out localEnd);
            string detail = "frame válido" + (hasEnd ? "; END global=" + (start + localEnd).ToString(CultureInfo.InvariantCulture) : "; END ausente nesta página");
            SaveHex(label + "-frame.hex", frame);
            Add(results, label, true, detail);
        }

        private void ValidateRead(SerialPort port, List<CheckResult> results, string label, byte[] request, int expected, bool decodeWord)
        {
            byte[] raw = ExchangeRaw(port, request, 4200, 280, label);
            byte[] frame = FindFrame(raw, expected);
            if (frame == null)
            {
                Add(results, label, false, "Resposta LEN=" + expected.ToString("X2", CultureInfo.InvariantCulture) + " não localizada");
                return;
            }
            string detail = ToHex(frame);
            if (decodeWord && expected == 2)
            {
                ushort[] words = Tp02PgMemoryProtocol.DecodeWords(frame, 1);
                detail += " => " + words[0].ToString(CultureInfo.InvariantCulture) + " (" + words[0].ToString("X4", CultureInfo.InvariantCulture) + "h)";
            }
            SaveHex(label + "-frame.hex", frame);
            Add(results, label, true, detail);
        }

        private void ValidateClock(SerialPort port, List<CheckResult> results)
        {
            byte[] raw = ExchangeRaw(port, Tp02PgMemoryProtocol.ReadClock(), 4200, 280, "0A-RTC");
            byte[] frame = FindFrame(raw, 14);
            if (frame == null) { Add(results, "0A-RTC", false, "Resposta LEN=0E não localizada"); return; }
            ushort[] v = Tp02PgMemoryProtocol.DecodeClock(frame);
            string detail = "seg=" + v[0] + " min=" + v[1] + " h=" + v[2] + " dia=" + v[3] + " dow=" + v[4] + " mes=" + v[5] + " ano=" + v[6];
            SaveHex("0A-RTC-frame.hex", frame);
            Add(results, "0A-RTC", true, detail);
        }

        private void ValidateScan(SerialPort port, List<CheckResult> results)
        {
            byte[] raw = ExchangeRaw(port, Tp02PgMemoryProtocol.ReadScanTimes(), 4200, 280, "0A-SCAN");
            byte[] frame = FindFrame(raw, 6);
            if (frame == null) { Add(results, "0A-SCAN", false, "Resposta LEN=06 não localizada"); return; }
            ushort[] v = Tp02PgMemoryProtocol.DecodeScanTimes(frame);
            string detail = "atual=" + v[0] + " minimo=" + v[1] + " maximo=" + v[2];
            SaveHex("0A-SCAN-frame.hex", frame);
            Add(results, "0A-SCAN", true, detail);
        }

        private byte[] ExchangeRaw(SerialPort port, byte[] request, int timeoutMs, int quietMs, string label)
        {
            if (!TP02PhysicalValidationSafety.IsAllowed(request))
                throw new InvalidOperationException("BLOQUEIO DE SEGURANÇA: quadro fora da allowlist READ-ONLY: " + ToHex(request));
            port.DiscardInBuffer();
            AppendSafe(label + " TX: " + ToHex(request));
            SaveHex(label + "-tx.hex", request);
            port.Write(request, 0, request.Length);
            byte[] raw = ReadBurst(port, timeoutMs, quietMs);
            AppendSafe(label + " RX: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
            SaveHex(label + "-rx.hex", raw);
            return raw;
        }

        private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs)
        {
            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            DateTime last = DateTime.MinValue;
            while (DateTime.UtcNow < deadline)
            {
                int count = port.BytesToRead;
                if (count > 0)
                {
                    byte[] chunk = new byte[count];
                    int got = port.Read(chunk, 0, chunk.Length);
                    for (int i = 0; i < got; i++) bytes.Add(chunk[i]);
                    last = DateTime.UtcNow;
                }
                else if (bytes.Count > 0 && last != DateTime.MinValue && (DateTime.UtcNow - last).TotalMilliseconds >= quietMs)
                    break;
                Thread.Sleep(15);
            }
            return bytes.ToArray();
        }

        private static byte[] FindFrame(byte[] raw, int expectedLen)
        {
            if (raw == null) return null;
            int total = expectedLen + 3;
            if (raw.Length < total) return null;
            for (int i = 0; i <= raw.Length - total; i++)
            {
                if (raw[i + 1] != (byte)expectedLen) continue;
                int sum = 0;
                for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total];
                Buffer.BlockCopy(raw, i, frame, 0, total);
                return frame;
            }
            return null;
        }

        private void Add(List<CheckResult> results, string name, bool pass, string detail)
        {
            CheckResult r = new CheckResult(); r.Name = name; r.Pass = pass; r.Detail = detail; results.Add(r);
            AppendSafe((pass ? "PASS " : "FAIL ") + name + " | " + detail);
        }

        private void WriteSummary(List<CheckResult> results, string hello)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("OpenLadder Studio - TP02 Physical Validation v1.48");
            text.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            text.AppendLine("Perfil: 19200 8O1 DTR=OFF RTS=OFF");
            text.AppendLine("HELLO: " + hello);
            text.AppendLine("Allowlist: CON-ICB, F0, 38, 34 e 0A somente.");
            text.AppendLine("Não enviados: 09, 33, 35, Clear, RUN, STOP, EEPROM.");
            text.AppendLine();
            foreach (CheckResult r in results) text.AppendLine((r.Pass ? "PASS" : "FAIL") + " | " + r.Name + " | " + r.Detail);
            File.WriteAllText(Path.Combine(sessionDir, "hardware-validation-summary.txt"), text.ToString(), Encoding.UTF8);
        }

        private void CreateSession()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenLadder Studio", "TP02 Physical Validation");
            sessionDir = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(sessionDir);
            sessionLog = Path.Combine(sessionDir, "session.log");
            folder.Enabled = true;
        }

        private void SaveHex(string name, byte[] data)
        {
            if (string.IsNullOrEmpty(sessionDir)) return;
            File.WriteAllText(Path.Combine(sessionDir, name), ToHex(data) + Environment.NewLine, Encoding.ASCII);
        }

        private void Append(string text)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + text;
            log.AppendText(line + Environment.NewLine);
            if (!string.IsNullOrEmpty(sessionLog)) File.AppendAllText(sessionLog, line + Environment.NewLine, Encoding.UTF8);
        }

        private void AppendSafe(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new MethodInvoker(delegate { Append(text); })); return; }
            Append(text);
        }

        private void SetBusy(bool value)
        {
            busy = value; ports.Enabled = !value; run.Enabled = !value; refresh.Enabled = !value; folder.Enabled = !value && !string.IsNullOrEmpty(sessionDir);
        }

        private void SetStatus(string text, Color color) { status.Text = text; status.ForeColor = color; }

        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(sessionDir) || !Directory.Exists(sessionDir)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", sessionDir); } catch { }
        }

        private static bool Contains(byte[] raw, byte[] sequence)
        {
            if (raw == null || sequence == null || sequence.Length == 0 || raw.Length < sequence.Length) return false;
            for (int i = 0; i <= raw.Length - sequence.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < sequence.Length; j++) if (raw[i + j] != sequence[j]) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            return BitConverter.ToString(data).Replace('-', ' ');
        }
    }
}

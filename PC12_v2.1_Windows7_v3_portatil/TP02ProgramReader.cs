using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class TP02RbpProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02ProgramReaderForm());
        }
    }

    internal sealed class TP02ProgramReaderForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);

        private ComboBox portCombo;
        private ComboBox baudCombo;
        private ComboBox parityCombo;
        private ComboBox dataBitsCombo;
        private ComboBox stopBitsCombo;
        private NumericUpDown stationBox;
        private NumericUpDown responseTimeBox;
        private NumericUpDown addressBox;
        private NumericUpDown stepsBox;
        private CheckBox doubleColonCheck;
        private TextBox outputBox;
        private string lastDump = string.Empty;

        public TP02ProgramReaderForm()
        {
            Text = "TP02 Program Reader - RBP";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 650);
            Size = new Size(1160, 760);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = LabelAt("LEITOR DE PROGRAMA TP02 — RBP", 15.0f, FontStyle.Bold, Navy, 22, 13);
            header.Controls.Add(title);
            Label sub = LabelAt("Leitura da memória de programa em linguagem de máquina, sem alterar o PLC.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43);
            header.Controls.Add(sub);

            Label safe = new Label();
            safe.Text = "SOMENTE LEITURA";
            safe.Dock = DockStyle.Right;
            safe.Width = 190;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 150;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Configuração serial", 12.0f, FontStyle.Bold, TextPrimary, 18, 14));

            AddField(config, "Porta", 18);
            portCombo = ComboAt(18, 72, 104);
            config.Controls.Add(portCombo);
            Button refresh = ButtonAt("ATUALIZAR", 130, 70, 96, false);
            refresh.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refresh);

            AddField(config, "Baud", 242);
            baudCombo = ComboAt(242, 72, 92);
            baudCombo.Items.AddRange(new object[] { "38400", "19200", "9600", "4800", "2400", "1200", "600", "300" });
            baudCombo.SelectedItem = "19200";
            config.Controls.Add(baudCombo);

            AddField(config, "Paridade", 350);
            parityCombo = ComboAt(350, 72, 92);
            parityCombo.Items.AddRange(new object[] { "Even", "Odd", "None" });
            parityCombo.SelectedItem = "Even";
            config.Controls.Add(parityCombo);

            AddField(config, "Bits", 458);
            dataBitsCombo = ComboAt(458, 72, 66);
            dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
            dataBitsCombo.SelectedItem = "7";
            config.Controls.Add(dataBitsCombo);

            AddField(config, "Stop", 540);
            stopBitsCombo = ComboAt(540, 72, 66);
            stopBitsCombo.Items.AddRange(new object[] { "2", "1" });
            stopBitsCombo.SelectedItem = "2";
            config.Controls.Add(stopBitsCombo);

            AddField(config, "Estação", 622);
            stationBox = NumericAt(622, 72, 68, 1, 99, 1);
            config.Controls.Add(stationBox);

            AddField(config, "Resposta", 706);
            responseTimeBox = NumericAt(706, 72, 68, 0, 15, 5);
            config.Controls.Add(responseTimeBox);

            doubleColonCheck = new CheckBox();
            doubleColonCheck.Text = "Prefixo ::";
            doubleColonCheck.AutoSize = true;
            doubleColonCheck.Location = new Point(792, 74);
            doubleColonCheck.ForeColor = TextSecondary;
            config.Controls.Add(doubleColonCheck);

            Label configNote = LabelAt("Padrão inicial: 19200 / 7 / EVEN / 2 / estação 01. O RBP pode ler até 100 passos por comando.", 8.4f, FontStyle.Regular, TextSecondary, 18, 112);
            config.Controls.Add(configNote);

            Panel read = new Panel();
            read.Dock = DockStyle.Top;
            read.Height = 130;
            read.BackColor = Canvas;
            Controls.Add(read);

            read.Controls.Add(LabelAt("Leitura da memória de programa", 11.0f, FontStyle.Bold, TextPrimary, 18, 14));

            Label a = LabelAt("Endereço inicial", 8.2f, FontStyle.Bold, TextSecondary, 18, 48);
            read.Controls.Add(a);
            addressBox = NumericAt(18, 70, 110, 0, 4000, 0);
            addressBox.Increment = 1;
            read.Controls.Add(addressBox);

            Label s = LabelAt("Passos", 8.2f, FontStyle.Bold, TextSecondary, 148, 48);
            read.Controls.Add(s);
            stepsBox = NumericAt(148, 70, 80, 1, 100, 10);
            read.Controls.Add(stepsBox);

            Button readBlock = ButtonAt("LER BLOCO RBP", 250, 66, 160, true);
            readBlock.Click += delegate { ReadBlock(); };
            read.Controls.Add(readBlock);

            Button first100 = ButtonAt("LER 0000–0099", 424, 66, 150, false);
            first100.Click += delegate { addressBox.Value = 0; stepsBox.Value = 100; ReadBlock(); };
            read.Controls.Add(first100);

            Button save = ButtonAt("SALVAR DUMP", 588, 66, 130, false);
            save.Click += delegate { SaveDump(); };
            read.Controls.Add(save);

            Button clear = ButtonAt("LIMPAR", 732, 66, 100, false);
            clear.Click += delegate { outputBox.Clear(); lastDump = string.Empty; };
            read.Controls.Add(clear);

            Label warning = LabelAt("O comando RBP apenas lê o programa. WBP, RUN, STOP e comandos de limpeza permanecem fora desta ferramenta.", 8.4f, FontStyle.Regular, TextSecondary, 18, 106);
            read.Controls.Add(warning);

            outputBox = new TextBox();
            outputBox.Dock = DockStyle.Fill;
            outputBox.Multiline = true;
            outputBox.ReadOnly = true;
            outputBox.WordWrap = false;
            outputBox.ScrollBars = ScrollBars.Both;
            outputBox.Font = new Font("Consolas", 9.4f);
            outputBox.BackColor = Color.FromArgb(20, 28, 36);
            outputBox.ForeColor = Color.FromArgb(220, 233, 245);
            Controls.Add(outputBox);

            header.BringToFront();
            config.BringToFront();
            read.BringToFront();
        }

        private void ReadBlock()
        {
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show("Nenhuma porta COM selecionada.", "TP02 Program Reader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int start = (int)addressBox.Value;
            int count = (int)stepsBox.Value;
            if (start + count - 1 > 4000)
            {
                MessageBox.Show("O bloco ultrapassa o endereço 4000 do TP02-40/60. Reduza o endereço inicial ou a quantidade.", "Faixa inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string payload = start.ToString("0000", CultureInfo.InvariantCulture) + (count == 100 ? "00" : count.ToString("00", CultureInfo.InvariantCulture));
            string frame = BuildFrame("RBP", payload);
            Append("TX", Escape(frame));

            SerialPort port = null;
            try
            {
                port = new SerialPort(portCombo.SelectedItem.ToString());
                port.BaudRate = int.Parse(baudCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
                port.DataBits = int.Parse(dataBitsCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
                port.Parity = (Parity)Enum.Parse(typeof(Parity), parityCombo.SelectedItem.ToString());
                port.StopBits = stopBitsCombo.SelectedItem.ToString() == "2" ? StopBits.Two : StopBits.One;
                port.Encoding = Encoding.ASCII;
                port.ReadTimeout = 3500;
                port.WriteTimeout = 1500;
                port.NewLine = "\r";
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                port.Write(frame);
                string response = port.ReadTo("\r") + "\r";
                Append("RX", Escape(response));
                DecodeRbpResponse(response, start, count);
            }
            catch (TimeoutException)
            {
                Append("ERRO", "Timeout. Confirme porta COM, estação, configuração serial e cabo/conversor.");
            }
            catch (Exception ex)
            {
                Append("ERRO", ex.Message);
            }
            finally
            {
                if (port != null)
                {
                    try { if (port.IsOpen) port.Close(); } catch { }
                    port.Dispose();
                }
            }
        }

        private void DecodeRbpResponse(string response, int requestedStart, int requestedCount)
        {
            string clean = (response ?? string.Empty).TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            if (clean.Length < 8)
            {
                Append("ERRO", "Resposta curta demais para decodificar.");
                return;
            }

            bool checksumOk = VerifyChecksum(clean);
            if (clean.IndexOf('%') >= 0)
            {
                Append("ERRO", "O TP02 retornou uma resposta de erro. Checksum " + (checksumOk ? "OK" : "não validado") + ".");
                return;
            }

            int marker = clean.IndexOf('#');
            if (marker < 0)
            {
                Append("ERRO", "Resposta sem marcador #.");
                return;
            }

            string body = clean.Substring(marker + 1, clean.Length - marker - 1 - 2); // sem checksum
            int rbp = body.IndexOf("RBP", StringComparison.OrdinalIgnoreCase);
            if (rbp >= 0) body = body.Substring(rbp + 3);
            else if (body.Length >= 4 && char.IsLetter(body[0]) && char.IsLetter(body[1]) && char.IsLetter(body[2])) body = body.Substring(3);

            string returnedCount = string.Empty;
            if (body.Length >= 2 && ((body.Length - 2) % 6 == 0))
            {
                returnedCount = body.Substring(body.Length - 2, 2);
                body = body.Substring(0, body.Length - 2);
            }

            int words = body.Length / 6;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("RBP — DUMP DE PROGRAMA");
            sb.AppendLine(new string('=', 72));
            sb.AppendLine("Solicitado: endereço " + requestedStart.ToString("0000") + ", " + requestedCount.ToString() + " passo(s)");
            sb.AppendLine("Checksum: " + (checksumOk ? "OK" : "NÃO VALIDADO"));
            if (returnedCount.Length > 0) sb.AppendLine("Contagem informada na resposta: " + returnedCount + (returnedCount == "00" ? " (100)" : string.Empty));
            sb.AppendLine("Palavras de máquina detectadas: " + words.ToString());
            sb.AppendLine();
            sb.AppendLine("PASSO  WORD    BYTE-H BYTE-L EXT");
            sb.AppendLine("-----  ------  ------ ------ ---");

            int i;
            for (i = 0; i < words; i++)
            {
                string word = body.Substring(i * 6, 6).ToUpperInvariant();
                sb.Append((requestedStart + i).ToString("0000")).Append("   ")
                  .Append(word).Append("  ")
                  .Append(word.Substring(0, 2)).Append("     ")
                  .Append(word.Substring(2, 2)).Append("     ")
                  .Append(word.Substring(4, 2)).AppendLine();
            }

            if (body.Length % 6 != 0)
            {
                sb.AppendLine();
                sb.AppendLine("Dados residuais não agrupados: " + body.Substring(words * 6));
            }

            sb.AppendLine();
            sb.AppendLine("Observação: cada passo RBP é retornado em 3 bytes (6 caracteres hexadecimais). A tradução destes bytes para STR/AND/OR/OUT/TMR/CNT será a próxima camada de decodificação.");

            lastDump = sb.ToString();
            outputBox.AppendText(lastDump + Environment.NewLine);
        }

        private string BuildFrame(string command, string payload)
        {
            string station = ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture);
            const string codes = "0123456789ABCDEF";
            char responseCode = codes[(int)responseTimeBox.Value];
            string core = station + "?" + responseCode + command + payload;
            string prefix = doubleColonCheck.Checked ? "::" : ":";
            return prefix + core + Checksum(core) + "\r";
        }

        private static string Checksum(string core)
        {
            int sum = 0;
            int i;
            for (i = 0; i < core.Length; i++) sum = (sum + (byte)core[i]) & 0xFF;
            return (((~sum) + 1) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static bool VerifyChecksum(string clean)
        {
            if (clean.Length < 3) return false;
            int parsed;
            if (!int.TryParse(clean.Substring(clean.Length - 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed)) return false;
            int sum = 0;
            int i;
            for (i = 0; i < clean.Length - 2; i++) sum = (sum + (byte)clean[i]) & 0xFF;
            return ((sum + parsed) & 0xFF) == 0;
        }

        private void SaveDump()
        {
            if (string.IsNullOrEmpty(lastDump))
            {
                MessageBox.Show("Faça uma leitura RBP antes de salvar.", "TP02 Program Reader", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt";
            dlg.FileName = "TP02_RBP_" + ((int)addressBox.Value).ToString("0000") + ".rbpdump";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, lastDump, Encoding.UTF8);
        }

        private void RefreshPorts()
        {
            string previous = portCombo == null || portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (ports.Length == 0) return;
            int idx = Array.IndexOf(ports, previous);
            portCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void Append(string kind, string text)
        {
            outputBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + kind + "  " + text + Environment.NewLine);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", "<CR>").Replace("\n", "<LF>");
        }

        private void AddField(Control parent, string text, int left)
        {
            parent.Controls.Add(LabelAt(text, 8.1f, FontStyle.Bold, TextSecondary, left, 50));
        }

        private ComboBox ComboAt(int left, int top, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(left, top);
            c.Size = new Size(width, 25);
            return c;
        }

        private NumericUpDown NumericAt(int left, int top, int width, int min, int max, int value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(left, top);
            n.Size = new Size(width, 25);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            return n;
        }

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 34);
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

        private Label LabelAt(string text, float size, FontStyle style, Color color, int left, int top)
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

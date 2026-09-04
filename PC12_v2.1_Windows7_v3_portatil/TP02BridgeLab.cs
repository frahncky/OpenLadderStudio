using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class TP02BridgeProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02BridgeForm());
        }
    }

    internal sealed class TP02BridgeForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private TextBox reportBox;
        private TextBox logBox;
        private ComboBox portCombo;
        private ComboBox baudCombo;
        private ComboBox parityCombo;
        private ComboBox dataBitsCombo;
        private ComboBox stopBitsCombo;
        private NumericUpDown stationBox;
        private NumericUpDown responseTimeBox;
        private CheckBox doubleColonCheck;
        private TextBox mcrAddressBox;
        private TextBox mrvAddressBox;
        private NumericUpDown mrvCountBox;
        private string currentReport = string.Empty;

        public TP02BridgeForm()
        {
            Text = "TP02 Bridge Lab";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 680);
            Size = new Size(1180, 760);
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
            header.Height = 70;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "TP02 BRIDGE LAB";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI Semibold", 15.0f, FontStyle.Bold);
            title.ForeColor = Navy;
            title.Location = new Point(22, 13);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Engenharia reversa controlada do PC12 + diagnóstico serial somente leitura";
            sub.AutoSize = true;
            sub.ForeColor = TextSecondary;
            sub.Location = new Point(24, 42);
            header.Controls.Add(sub);

            Label safety = new Label();
            safety.Text = "MODO SEGURO: nenhum comando de escrita, RUN, STOP ou limpeza de memória é enviado.";
            safety.AutoSize = false;
            safety.TextAlign = ContentAlignment.MiddleRight;
            safety.Dock = DockStyle.Right;
            safety.Width = 520;
            safety.Padding = new Padding(0, 0, 24, 0);
            safety.ForeColor = Color.FromArgb(27, 132, 86);
            safety.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            header.Controls.Add(safety);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI Semibold", 9.0f);
            Controls.Add(tabs);
            DockOrder.Apply(this, tabs, header);

            TabPage projectTab = new TabPage("Projeto PC12");
            projectTab.BackColor = Canvas;
            tabs.TabPages.Add(projectTab);
            BuildProjectTab(projectTab);

            TabPage serialTab = new TabPage("Comunicação TP02");
            serialTab.BackColor = Canvas;
            tabs.TabPages.Add(serialTab);
            BuildSerialTab(serialTab);

        }

        private void BuildProjectTab(Control parent)
        {
            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 66;
            commands.BackColor = Canvas;
            parent.Controls.Add(commands);

            Button analyze = ActionButton("ANALISAR PROJETO PC12", 18, 14, 190);
            analyze.Click += delegate { AnalyzeLegacyProject(); };
            commands.Controls.Add(analyze);

            Button compare = ActionButton("COMPARAR DOIS ARQUIVOS", 220, 14, 190);
            compare.Click += delegate { CompareLegacyFiles(); };
            commands.Controls.Add(compare);

            Button save = ActionButton("SALVAR RELATÓRIO", 422, 14, 150);
            save.Click += delegate { SaveReport(); };
            commands.Controls.Add(save);

            Label help = new Label();
            help.Text = "Selecione o arquivo .PLC. O laboratório procura automaticamente os arquivos auxiliares do mesmo projeto.";
            help.AutoSize = true;
            help.ForeColor = TextSecondary;
            help.Location = new Point(590, 25);
            commands.Controls.Add(help);

            reportBox = new TextBox();
            reportBox.Dock = DockStyle.Fill;
            reportBox.Multiline = true;
            reportBox.ScrollBars = ScrollBars.Both;
            reportBox.ReadOnly = true;
            reportBox.WordWrap = false;
            reportBox.Font = new Font("Consolas", 9.2f);
            reportBox.BackColor = Color.White;
            reportBox.ForeColor = TextPrimary;
            parent.Controls.Add(reportBox);
            DockOrder.Apply(parent, reportBox, commands);
        }

        private void BuildSerialTab(Control parent)
        {
            Panel settings = new Panel();
            settings.Dock = DockStyle.Top;
            settings.Height = 164;
            settings.BackColor = Color.White;
            parent.Controls.Add(settings);

            Label title = NewLabel("Configuração serial", 14.0f, FontStyle.Bold, Navy, 18, 14);
            settings.Controls.Add(title);

            AddFieldLabel(settings, "Porta", 18, 54);
            portCombo = NewCombo(18, 76, 110);
            settings.Controls.Add(portCombo);
            Button refresh = ActionButton("ATUALIZAR", 136, 75, 100);
            refresh.Click += delegate { RefreshPorts(); };
            settings.Controls.Add(refresh);

            AddFieldLabel(settings, "Baud", 252, 54);
            baudCombo = NewCombo(252, 76, 100);
            baudCombo.Items.AddRange(new object[] { "38400", "19200", "9600", "4800", "2400", "1200", "600", "300" });
            baudCombo.SelectedItem = "19200";
            settings.Controls.Add(baudCombo);

            AddFieldLabel(settings, "Paridade", 366, 54);
            parityCombo = NewCombo(366, 76, 100);
            parityCombo.Items.AddRange(new object[] { "Even", "Odd", "None" });
            parityCombo.SelectedItem = "Even";
            settings.Controls.Add(parityCombo);

            AddFieldLabel(settings, "Bits", 480, 54);
            dataBitsCombo = NewCombo(480, 76, 72);
            dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
            dataBitsCombo.SelectedItem = "7";
            settings.Controls.Add(dataBitsCombo);

            AddFieldLabel(settings, "Stop", 566, 54);
            stopBitsCombo = NewCombo(566, 76, 72);
            stopBitsCombo.Items.AddRange(new object[] { "2", "1" });
            stopBitsCombo.SelectedItem = "2";
            settings.Controls.Add(stopBitsCombo);

            AddFieldLabel(settings, "Estação", 652, 54);
            stationBox = NewNumeric(652, 76, 70, 1, 99, 1);
            settings.Controls.Add(stationBox);

            AddFieldLabel(settings, "Resposta", 736, 54);
            responseTimeBox = NewNumeric(736, 76, 70, 0, 15, 5);
            settings.Controls.Add(responseTimeBox);
            Label rt = NewLabel("5 = 50 ms", 8.0f, FontStyle.Regular, TextSecondary, 736, 106);
            settings.Controls.Add(rt);

            doubleColonCheck = new CheckBox();
            doubleColonCheck.Text = "Compatibilidade com prefixo ::";
            doubleColonCheck.AutoSize = true;
            doubleColonCheck.Location = new Point(830, 78);
            doubleColonCheck.ForeColor = TextSecondary;
            settings.Controls.Add(doubleColonCheck);

            Label note = NewLabel("Padrão inicial: 19200 bps, 7 bits, paridade EVEN, 2 stop bits, estação 01. Ajuste conforme WS041/WS042 do seu TP02.", 8.6f, FontStyle.Regular, TextSecondary, 18, 130);
            settings.Controls.Add(note);

            Panel actions = new Panel();
            actions.Dock = DockStyle.Top;
            actions.Height = 152;
            actions.BackColor = Canvas;
            parent.Controls.Add(actions);

            Label psr = NewLabel("1. Teste de comunicação", 10.0f, FontStyle.Bold, TextPrimary, 18, 16);
            actions.Controls.Add(psr);
            Button status = PrimaryButton("LER STATUS (PSR)", 18, 42, 170);
            status.Click += delegate { ExecuteRead("PSR", string.Empty); };
            actions.Controls.Add(status);

            Label coil = NewLabel("2. Ler contato/bobina", 10.0f, FontStyle.Bold, TextPrimary, 220, 16);
            actions.Controls.Add(coil);
            mcrAddressBox = new TextBox();
            mcrAddressBox.Text = "C0001";
            mcrAddressBox.CharacterCasing = CharacterCasing.Upper;
            mcrAddressBox.Font = new Font("Consolas", 10.0f, FontStyle.Bold);
            mcrAddressBox.Location = new Point(220, 44);
            mcrAddressBox.Size = new Size(108, 25);
            actions.Controls.Add(mcrAddressBox);
            Button mcr = ActionButton("LER MCR", 338, 42, 105);
            mcr.Click += delegate
            {
                string address = NormalizeBitAddress(mcrAddressBox.Text);
                if (address == null) { MessageBox.Show("Endereço inválido. Exemplos: X0001, Y0001, C0001, SC001."); return; }
                mcrAddressBox.Text = address;
                ExecuteRead("MCR", address);
            };
            actions.Controls.Add(mcr);

            Label reg = NewLabel("3. Ler registrador", 10.0f, FontStyle.Bold, TextPrimary, 474, 16);
            actions.Controls.Add(reg);
            mrvAddressBox = new TextBox();
            mrvAddressBox.Text = "D0001";
            mrvAddressBox.CharacterCasing = CharacterCasing.Upper;
            mrvAddressBox.Font = new Font("Consolas", 10.0f, FontStyle.Bold);
            mrvAddressBox.Location = new Point(474, 44);
            mrvAddressBox.Size = new Size(108, 25);
            actions.Controls.Add(mrvAddressBox);
            mrvCountBox = NewNumeric(590, 44, 62, 1, 99, 1);
            actions.Controls.Add(mrvCountBox);
            Button mrv = ActionButton("LER MRV", 662, 42, 105);
            mrv.Click += delegate
            {
                string address = NormalizeWordAddress(mrvAddressBox.Text);
                if (address == null) { MessageBox.Show("Endereço inválido. Exemplos: V0001, D0001, WS001, WC001, F0001."); return; }
                mrvAddressBox.Text = address;
                string count = ((int)mrvCountBox.Value).ToString("00", CultureInfo.InvariantCulture);
                ExecuteRead("MRV", address + count);
            };
            actions.Controls.Add(mrv);

            Label warning = NewLabel("Somente comandos de leitura estão habilitados nesta etapa: PSR, MCR e MRV. RBP será liberado após validar a codificação do programa Boolean.", 8.7f, FontStyle.Regular, TextSecondary, 18, 92);
            actions.Controls.Add(warning);

            Button clear = ActionButton("LIMPAR LOG", 18, 116, 120);
            clear.Click += delegate { logBox.Clear(); };
            actions.Controls.Add(clear);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.Font = new Font("Consolas", 9.2f);
            logBox.BackColor = Color.FromArgb(20, 28, 36);
            logBox.ForeColor = Color.FromArgb(218, 232, 245);
            parent.Controls.Add(logBox);
            DockOrder.Apply(parent, logBox, actions, settings);

        }

        private Button ActionButton(string text, int left, int top, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(194, 205, 216);
            b.BackColor = Color.White;
            b.ForeColor = Navy;
            b.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private Button PrimaryButton(string text, int left, int top, int width)
        {
            Button b = ActionButton(text, left, top, width);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
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

        private void AddFieldLabel(Control parent, string text, int left, int top)
        {
            Label l = NewLabel(text, 8.2f, FontStyle.Bold, TextSecondary, left, top);
            parent.Controls.Add(l);
        }

        private ComboBox NewCombo(int left, int top, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(left, top);
            c.Size = new Size(width, 25);
            return c;
        }

        private NumericUpDown NewNumeric(int left, int top, int width, int min, int max, int value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(left, top);
            n.Size = new Size(width, 25);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            return n;
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            string previous = portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (ports.Length == 0) return;
            int index = Array.IndexOf(ports, previous);
            portCombo.SelectedIndex = index >= 0 ? index : 0;
        }

        private void AnalyzeLegacyProject()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Projeto PC12 (*.PLC)|*.PLC|Todos os arquivos (*.*)|*.*";
            dlg.Title = "Selecione o arquivo principal .PLC";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string basePath = Path.Combine(Path.GetDirectoryName(dlg.FileName), Path.GetFileNameWithoutExtension(dlg.FileName));
            string[] suffixes = new string[] { ".PLC", ".sys1", ".sys2", ".cnt", ".reg1", ".reg2", ".reg3", ".sym", ".file", ".cmt", ".typ" };
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TP02 BRIDGE LAB - RELATÓRIO DE PROJETO PC12");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine("Projeto-base: " + basePath);
            sb.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("ARQUIVOS ESPERADOS PELO PC12");
            sb.AppendLine(new string('-', 70));

            List<string> found = new List<string>();
            int i;
            for (i = 0; i < suffixes.Length; i++)
            {
                string path = basePath + suffixes[i];
                bool exists = File.Exists(path);
                if (!exists && suffixes[i] == ".PLC") path = dlg.FileName;
                exists = File.Exists(path);
                if (exists) found.Add(path);
                sb.AppendLine(string.Format("{0,-7} {1,-10} {2}", suffixes[i], exists ? new FileInfo(path).Length.ToString() + " bytes" : "AUSENTE", exists ? Path.GetFileName(path) : string.Empty));
            }

            sb.AppendLine();
            sb.AppendLine("ANÁLISE DOS ARQUIVOS ENCONTRADOS");
            sb.AppendLine(new string('=', 70));
            for (i = 0; i < found.Count; i++)
            {
                AppendFileAnalysis(sb, found[i]);
            }

            currentReport = sb.ToString();
            reportBox.Text = currentReport;
        }

        private static void AppendFileAnalysis(StringBuilder sb, string path)
        {
            byte[] data = File.ReadAllBytes(path);
            sb.AppendLine();
            sb.AppendLine("[" + Path.GetFileName(path) + "]");
            sb.AppendLine("Tamanho: " + data.Length.ToString() + " bytes");
            sb.AppendLine("SHA-256: " + Sha256(data));
            sb.AppendLine("Perfil: " + (LooksTextual(data) ? "predominantemente textual" : "binário/estruturado"));
            sb.AppendLine("Strings legíveis: " + ExtractStrings(data, 4, 24));
            sb.AppendLine("Primeiros bytes:");
            sb.AppendLine(HexDump(data, 0, Math.Min(256, data.Length)));
        }

        private void CompareLegacyFiles()
        {
            OpenFileDialog a = new OpenFileDialog();
            a.Title = "Arquivo A - referência";
            a.Filter = "Arquivos PC12 (*.*)|*.*";
            if (a.ShowDialog(this) != DialogResult.OK) return;
            OpenFileDialog b = new OpenFileDialog();
            b.Title = "Arquivo B - modificado";
            b.Filter = "Arquivos PC12 (*.*)|*.*";
            if (b.ShowDialog(this) != DialogResult.OK) return;

            byte[] left = File.ReadAllBytes(a.FileName);
            byte[] right = File.ReadAllBytes(b.FileName);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TP02 BRIDGE LAB - COMPARAÇÃO BINÁRIA");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine("A: " + a.FileName + " (" + left.Length.ToString() + " bytes)");
            sb.AppendLine("B: " + b.FileName + " (" + right.Length.ToString() + " bytes)");
            sb.AppendLine();

            int max = Math.Max(left.Length, right.Length);
            int changed = 0;
            int segments = 0;
            int p = 0;
            while (p < max)
            {
                byte av = p < left.Length ? left[p] : (byte)0;
                byte bv = p < right.Length ? right[p] : (byte)0;
                bool different = p >= left.Length || p >= right.Length || av != bv;
                if (!different) { p++; continue; }
                int start = p;
                while (p < max)
                {
                    av = p < left.Length ? left[p] : (byte)0;
                    bv = p < right.Length ? right[p] : (byte)0;
                    if (p < left.Length && p < right.Length && av == bv) break;
                    changed++;
                    p++;
                }
                int end = p - 1;
                if (segments < 200) sb.AppendLine("Diferença 0x" + start.ToString("X6") + " - 0x" + end.ToString("X6") + " (" + (end - start + 1).ToString() + " bytes)");
                segments++;
            }

            sb.AppendLine();
            sb.AppendLine("Bytes diferentes/adicionados/removidos: " + changed.ToString());
            sb.AppendLine("Faixas de diferença: " + segments.ToString());
            if (segments > 200) sb.AppendLine("Relatório limitado às primeiras 200 faixas.");
            sb.AppendLine();
            sb.AppendLine("DICA DE ENGENHARIA REVERSA:");
            sb.AppendLine("Crie dois projetos PC12 idênticos e altere somente UM elemento (por exemplo X0001 -> X0002). Compare os .PLC e repita para OUT, TMR, CNT e FUN.");

            currentReport = sb.ToString();
            reportBox.Text = currentReport;
        }

        private void SaveReport()
        {
            if (string.IsNullOrEmpty(currentReport)) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Relatório texto (*.txt)|*.txt";
            dlg.FileName = "TP02_Bridge_Relatorio.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, currentReport, Encoding.UTF8);
        }

        private void ExecuteRead(string command, string payload)
        {
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show("Nenhuma porta COM selecionada.", "TP02 Bridge Lab", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string frame = BuildFrame(command, payload);
            Log("TX", EscapeFrame(frame));
            SerialPort port = null;
            try
            {
                port = new SerialPort(portCombo.SelectedItem.ToString());
                port.BaudRate = int.Parse(baudCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
                port.DataBits = int.Parse(dataBitsCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
                port.Parity = (Parity)Enum.Parse(typeof(Parity), parityCombo.SelectedItem.ToString());
                port.StopBits = stopBitsCombo.SelectedItem.ToString() == "2" ? StopBits.Two : StopBits.One;
                port.Encoding = Encoding.ASCII;
                port.ReadTimeout = 2500;
                port.WriteTimeout = 1500;
                port.NewLine = "\r";
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                port.Write(frame);
                string response = port.ReadTo("\r") + "\r";
                Log("RX", EscapeFrame(response));
                Log("OK", DecodeResponse(command, response));
            }
            catch (TimeoutException)
            {
                Log("ERRO", "Timeout: o TP02 não respondeu. Confirme COM, estação, cabo/conversor, WS041/WS042 e tente o prefixo :: se necessário.");
            }
            catch (Exception ex)
            {
                Log("ERRO", ex.Message);
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

        private string BuildFrame(string command, string payload)
        {
            string station = ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture);
            const string responseCodes = "0123456789ABCDEF";
            char responseCode = responseCodes[(int)responseTimeBox.Value];
            string core = station + "?" + responseCode + command + payload;
            string prefix = doubleColonCheck.Checked ? "::" : ":";
            return prefix + core + Checksum(core) + "\r";
        }

        private static string Checksum(string core)
        {
            int sum = 0;
            int i;
            for (i = 0; i < core.Length; i++) sum = (sum + (byte)core[i]) & 0xFF;
            int checksum = ((~sum) + 1) & 0xFF;
            return checksum.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string DecodeResponse(string command, string response)
        {
            if (string.IsNullOrEmpty(response)) return "Resposta vazia.";
            string clean = response.TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            if (clean.Length < 8) return "Resposta curta; conteúdo bruto mantido no log.";

            bool checksumOk = VerifyChecksum(clean);
            int error = clean.IndexOf('%');
            if (error >= 0)
            {
                string code = clean.Length >= 2 ? clean.Substring(clean.Length - 4, 2) : string.Empty;
                return "Resposta de erro TP02" + (code.Length == 2 ? " (código possível " + code + ": " + ErrorText(code) + ")" : string.Empty) + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            }

            int marker = clean.IndexOf('#');
            if (marker < 0) return "Resposta sem marcador #. Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            int cmd = clean.IndexOf(command, marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0) return "Resposta normal recebida, mas o eco do comando não foi localizado. Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            int dataStart = cmd + command.Length;
            int dataLength = clean.Length - dataStart - 2;
            if (dataLength < 0) dataLength = 0;
            string data = clean.Substring(dataStart, dataLength);

            if (command == "PSR")
            {
                string state = data.Length > 0 ? data.Substring(0, 1) : "?";
                string meaning = state == "0" ? "STOP/PROGRAM" : state == "1" ? "RUN" : state == "2" ? "ERROR" : "desconhecido";
                return "PLC status = " + state + " (" + meaning + "). Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            }
            if (command == "MCR")
            {
                string state = data.Length > 0 ? data.Substring(0, 1) : "?";
                return "Estado = " + state + (state == "1" ? " (ON)" : state == "0" ? " (OFF)" : string.Empty) + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            }
            if (command == "MRV")
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Dados = ").Append(data);
                if (data.Length >= 4 && data.Length % 4 == 0)
                {
                    sb.Append(" | palavras: ");
                    int i;
                    for (i = 0; i < data.Length; i += 4)
                    {
                        int value;
                        if (int.TryParse(data.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append("0x").Append(data.Substring(i, 4)).Append("=").Append(value);
                        }
                    }
                }
                sb.Append(". Checksum ").Append(checksumOk ? "OK" : "não validado").Append('.');
                return sb.ToString();
            }
            return "Resposta recebida. Checksum " + (checksumOk ? "OK" : "não validado") + ".";
        }

        private static bool VerifyChecksum(string clean)
        {
            if (clean.Length < 3) return false;
            string checksumText = clean.Substring(clean.Length - 2, 2);
            int checksum;
            if (!int.TryParse(checksumText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum)) return false;
            int sum = 0;
            int i;
            for (i = 0; i < clean.Length - 2; i++) sum = (sum + (byte)clean[i]) & 0xFF;
            return ((sum + checksum) & 0xFF) == 0;
        }

        private static string ErrorText(string code)
        {
            if (code == "01") return "frame error";
            if (code == "02") return "operação de escrita bloqueada em RUN";
            if (code == "03") return "checksum incorreto";
            if (code == "04") return "endereço/intervalo fora da faixa";
            if (code == "05") return "falha de EEPROM";
            if (code == "06") return "senha ativa";
            return "erro não identificado";
        }

        private void Log(string kind, string message)
        {
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + kind + "  " + message + Environment.NewLine);
        }

        private static string EscapeFrame(string frame)
        {
            return frame.Replace("\r", "<CR>").Replace("\n", "<LF>");
        }

        private static string NormalizeBitAddress(string value)
        {
            string v = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string prefix;
            string digits;
            int width;
            if (v.StartsWith("SC")) { prefix = "SC"; digits = v.Substring(2); width = 3; }
            else if (v.StartsWith("X") || v.StartsWith("Y") || v.StartsWith("C")) { prefix = v.Substring(0, 1); digits = v.Substring(1); width = 4; }
            else return null;
            int n;
            if (!int.TryParse(digits, out n) || n < 0) return null;
            return prefix + n.ToString(new string('0', width), CultureInfo.InvariantCulture);
        }

        private static string NormalizeWordAddress(string value)
        {
            string v = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string prefix;
            string digits;
            int width;
            if (v.StartsWith("WS") || v.StartsWith("WC")) { prefix = v.Substring(0, 2); digits = v.Substring(2); width = 3; }
            else if (v.StartsWith("V") || v.StartsWith("D") || v.StartsWith("F")) { prefix = v.Substring(0, 1); digits = v.Substring(1); width = 4; }
            else return null;
            int n;
            if (!int.TryParse(digits, out n) || n < 0) return null;
            return prefix + n.ToString(new string('0', width), CultureInfo.InvariantCulture);
        }

        private static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                int i;
                for (i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool LooksTextual(byte[] data)
        {
            if (data.Length == 0) return true;
            int printable = 0;
            int sample = Math.Min(data.Length, 4096);
            int i;
            for (i = 0; i < sample; i++)
            {
                byte b = data[i];
                if (b == 9 || b == 10 || b == 13 || (b >= 32 && b <= 126)) printable++;
            }
            return printable >= sample * 0.80;
        }

        private static string ExtractStrings(byte[] data, int minLength, int maxStrings)
        {
            List<string> strings = new List<string>();
            StringBuilder current = new StringBuilder();
            int i;
            for (i = 0; i < data.Length && strings.Count < maxStrings; i++)
            {
                byte b = data[i];
                if (b >= 32 && b <= 126) current.Append((char)b);
                else
                {
                    if (current.Length >= minLength) strings.Add(current.ToString());
                    current.Length = 0;
                }
            }
            if (current.Length >= minLength && strings.Count < maxStrings) strings.Add(current.ToString());
            return strings.Count == 0 ? "(nenhuma)" : string.Join(" | ", strings.ToArray());
        }

        private static string HexDump(byte[] data, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            int end = Math.Min(data.Length, offset + count);
            int pos;
            for (pos = offset; pos < end; pos += 16)
            {
                sb.Append(pos.ToString("X6")).Append("  ");
                int j;
                for (j = 0; j < 16; j++)
                {
                    int idx = pos + j;
                    if (idx < end) sb.Append(data[idx].ToString("X2")).Append(' ');
                    else sb.Append("   ");
                }
                sb.Append(" ");
                for (j = 0; j < 16 && pos + j < end; j++)
                {
                    byte b = data[pos + j];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}

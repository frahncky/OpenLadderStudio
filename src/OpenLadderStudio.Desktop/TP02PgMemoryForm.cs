using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    internal sealed class TP02PgMemoryForm : Form
    {
        private readonly ComboBox operation = new ComboBox();
        private readonly ComboBox area = new ComboBox();
        private readonly NumericUpDown number = new NumericUpDown();
        private readonly NumericUpDown quantity = new NumericUpDown();
        private readonly TextBox input = new TextBox();
        private readonly TextBox output = new TextBox();
        private readonly Label hint = new Label();

        internal TP02PgMemoryForm()
        {
            Text = "OpenLadder Studio - Memória e relógio TP02";
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6, 13);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(850, 620);
            Size = new Size(1000, 740);
            Font = new Font("Segoe UI", 9);
            BackColor = OpenLadderPalette.Shell;
            ForeColor = OpenLadderPalette.Fore;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);
            layout.ColumnCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowCount = 7;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            Label description = new Label();
            description.Text = "Monte quadros ou interprete uma resposta capturada. Esta janela funciona sem conexão com o PLC.";
            description.AutoSize = true;
            description.MaximumSize = new Size(760, 0);
            description.Margin = new Padding(0, 0, 0, 12);
            layout.Controls.Add(description, 0, 0);

            FlowLayoutPanel options = new FlowLayoutPanel();
            options.AutoSize = true;
            options.Dock = DockStyle.Fill;
            options.WrapContents = true;
            operation.DropDownStyle = ComboBoxStyle.DropDownList;
            operation.Width = 245;
            operation.Items.AddRange(new object[] { "Ler área completa", "Ler intervalo", "Leituras múltiplas",
                "Escrever registradores", "SET de bit", "RESET de bit", "Escrever arquivo FL",
                "Escrever grupos SC", "Ler relógio", "Escrever relógio", "Ler tempos de varredura",
                "Interpretar registradores", "Interpretar relógio", "Interpretar tempos de varredura" });
            area.DropDownStyle = ComboBoxStyle.DropDownList;
            area.Width = 65;
            foreach (Tp02PgMemoryProtocol.Area item in Enum.GetValues(typeof(Tp02PgMemoryProtocol.Area))) area.Items.Add(item);
            area.SelectedItem = Tp02PgMemoryProtocol.Area.V;
            number.Minimum = 1; number.Maximum = 2048; number.Value = 1; number.Width = 75;
            quantity.Minimum = 1; quantity.Maximum = 255; quantity.Value = 2; quantity.Width = 70;
            options.Controls.Add(operation);
            options.Controls.Add(Caption("Área")); options.Controls.Add(area);
            options.Controls.Add(Caption("Número")); options.Controls.Add(number);
            options.Controls.Add(Caption("Bytes")); options.Controls.Add(quantity);
            layout.Controls.Add(options, 0, 1);

            hint.AutoSize = true;
            hint.MaximumSize = new Size(760, 0);
            hint.Margin = new Padding(0, 8, 0, 8);
            hint.ForeColor = OpenLadderPalette.Muted;
            layout.Controls.Add(hint, 0, 2);
            ConfigureText(input, false);
            layout.Controls.Add(input, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.Dock = DockStyle.Fill;
            Button generate = new Button();
            generate.Text = "Gerar / interpretar"; generate.AutoSize = true;
            generate.FlatStyle = FlatStyle.Flat;
            generate.FlatAppearance.BorderSize = 0;
            generate.BackColor = OpenLadderPalette.Accent;
            generate.ForeColor = OpenLadderPalette.OnAccent;
            generate.Click += delegate { Generate(); };
            actions.Controls.Add(generate);
            Button copy = new Button();
            copy.Text = "Copiar resultado"; copy.AutoSize = true;
            copy.FlatStyle = FlatStyle.Flat;
            copy.FlatAppearance.BorderColor = OpenLadderPalette.Border;
            copy.BackColor = OpenLadderPalette.Chrome;
            copy.ForeColor = OpenLadderPalette.Fore;
            copy.Click += delegate { if (output.TextLength > 0) Clipboard.SetText(output.Text); };
            actions.Controls.Add(copy);
            layout.Controls.Add(actions, 0, 4);
            ConfigureText(output, true);
            layout.Controls.Add(output, 0, 5);

            Label footer = new Label();
            footer.AutoSize = true;
            footer.MaximumSize = new Size(760, 0);
            footer.Text = "Relógio: ano com dois dígitos, sem inferir século; dia da semana de 0 (domingo) a 6 (sábado).";
            footer.ForeColor = OpenLadderPalette.Muted;
            footer.Margin = new Padding(0, 8, 0, 0);
            layout.Controls.Add(footer, 0, 6);
            operation.SelectedIndexChanged += delegate { UpdateInputs(); };
            area.SelectedIndexChanged += delegate { UpdateNumberLimit(); };
            operation.SelectedIndex = 0;
            OpenLadderPalette.Skin(this);
        }

        private Tp02PgMemoryProtocol.Area SelectedArea
        {
            get { return (Tp02PgMemoryProtocol.Area)area.SelectedItem; }
        }

        private static Label Caption(string text)
        {
            Label label = new Label(); label.Text = text; label.AutoSize = true;
            label.Margin = new Padding(8, 6, 2, 0); return label;
        }

        private static void ConfigureText(TextBox box, bool readOnly)
        {
            box.Multiline = true; box.ReadOnly = readOnly; box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false; box.Dock = DockStyle.Fill;
            box.Font = new Font("Consolas", 10);
            box.BackColor = OpenLadderPalette.ChromeLight; box.ForeColor = OpenLadderPalette.Fore;
        }

        private void UpdateInputs()
        {
            int op = operation.SelectedIndex;
            UpdateNumberLimit();
            area.Enabled = op <= 5;
            number.Enabled = op == 1 || op == 4 || op == 5 || op == 6;
            quantity.Enabled = op == 1;
            input.Enabled = op == 2 || op == 3 || op == 6 || op == 7 || op == 9 || op >= 11;
            string[] hints = {
                "Páginas do PC12: V/D com 64 registradores; WC com 57; FL com 10 arquivos. WS usa as duas consultas originais.",
                "Número iniciado em 1. Quantidade em bytes: registradores usam múltiplos de 2; FL usa múltiplos de 20.",
                "Uma leitura por linha: número;bytes. Todos os endereços usam a área selecionada.",
                "Um registrador por linha: número=valor hexadecimal (0000–FFFF). Áreas V/D/WC ou seleção conhecida de WS.",
                "PG35: liga um bit X, Y ou C.", "PG35: desliga um bit X, Y ou C.",
                "Cada arquivo FL contém 20 bytes. Informe pares hexadecimais separados por espaços.",
                "Dois bytes hexadecimais: SC001–008 e SC017–023. Cada byte substitui o grupo inteiro; o segundo vai de 00 a 7F.",
                "Lê os sete registradores V1018–V1024, incluindo o dia da semana.",
                "Sete números decimais: segundo minuto hora dia dia-da-semana mês ano. Exemplo: 59 34 12 25 4 8 26.",
                "Lê os tempos atual, mínimo e máximo, nessa ordem.",
                "Cole um quadro RX completo, em hexadecimal: status, tamanho, dados e checksum.",
                "Cole a resposta completa de 14 bytes de dados do RTC (17 bytes incluindo cabeçalho/checksum).",
                "Cole a resposta completa de 6 bytes de dados de varredura (9 bytes ao todo)."
            };
            hint.Text = hints[op];
            output.Clear();
        }

        private void UpdateNumberLimit()
        {
            number.Maximum = Tp02PgMemoryProtocol.Limit(operation.SelectedIndex == 6
                ? Tp02PgMemoryProtocol.Area.FL : SelectedArea);
        }

        private static byte[] ParseHex(string text)
        {
            string[] parts = text.Replace('-', ' ').Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length != 2 || !byte.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                    throw new FormatException("Use pares hexadecimais separados por espaços.");
            return bytes;
        }

        private static string Hex(byte[] frame) { return BitConverter.ToString(frame).Replace('-', ' '); }

        private void Generate()
        {
            output.Clear();
            try
            {
                List<byte[]> frames = new List<byte[]>();
                int op = operation.SelectedIndex;
                if (op == 0) frames.AddRange(Tp02PgMemoryProtocol.ReadAll(SelectedArea));
                else if (op == 1) frames.Add(Tp02PgMemoryProtocol.BuildRead(SelectedArea, (int)number.Value, (int)quantity.Value));
                else if (op == 2 || op == 3)
                {
                    List<int> numbers = new List<int>(); List<ushort> values = new List<ushort>();
                    List<byte[]> requests = new List<byte[]>();
                    foreach (string line in input.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = line.Split(op == 2 ? ';' : '=');
                        if (pair.Length != 2) throw new FormatException(op == 2 ? "Use número;bytes por linha." : "Use número=valor por linha.");
                        int n = int.Parse(pair[0].Trim(), CultureInfo.InvariantCulture);
                        if (op == 2) requests.Add(Tp02PgMemoryProtocol.BuildRead(SelectedArea, n, int.Parse(pair[1].Trim(), CultureInfo.InvariantCulture)));
                        else { numbers.Add(n); values.Add(ushort.Parse(pair[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); }
                    }
                    if (op == 2) frames.Add(Tp02PgMemoryProtocol.BuildMultipleRead(requests));
                    else frames.AddRange(Tp02PgMemoryProtocol.BuildRegisterWrites(SelectedArea, numbers, values));
                }
                else if (op == 4 || op == 5) frames.Add(Tp02PgMemoryProtocol.BuildSetReset(SelectedArea, (int)number.Value, op == 4));
                else if (op == 6) frames.Add(Tp02PgMemoryProtocol.BuildFileWrite((int)number.Value, ParseHex(input.Text)));
                else if (op == 7)
                {
                    byte[] groups = ParseHex(input.Text);
                    if (groups.Length != 2) throw new FormatException("Informe os dois bytes dos grupos SC.");
                    frames.AddRange(Tp02PgMemoryProtocol.BuildSystemCoils(groups[0], groups[1]));
                }
                else if (op == 8) frames.Add(Tp02PgMemoryProtocol.ReadClock());
                else if (op == 9)
                {
                    string[] parts = input.Text.Split(new char[] { ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int[] fields = new int[parts.Length];
                    for (int i = 0; i < fields.Length; i++) fields[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
                    frames.Add(Tp02PgMemoryProtocol.BuildClockWrite(fields));
                }
                else if (op == 10) frames.Add(Tp02PgMemoryProtocol.ReadScanTimes());
                else
                {
                    byte[] response = ParseHex(input.Text);
                    ushort[] values;
                    string[] labels;
                    if (op == 12)
                    {
                        values = Tp02PgMemoryProtocol.DecodeClock(response);
                        labels = new string[] { "Segundo", "Minuto", "Hora", "Dia", "Dia da semana", "Mês", "Ano (sem século)" };
                    }
                    else if (op == 13)
                    {
                        values = Tp02PgMemoryProtocol.DecodeScanTimes(response);
                        labels = new string[] { "Atual (ms)", "Mínimo (ms)", "Máximo (ms)" };
                    }
                    else
                    {
                        byte[] payload = Tp02PgMemoryProtocol.Payload(response, -1);
                        if (payload.Length == 0 || payload.Length % 2 != 0) throw new FormatException("Dados não contêm registradores completos.");
                        values = Tp02PgMemoryProtocol.DecodeWords(response, payload.Length / 2);
                        labels = new string[values.Length];
                        for (int i = 0; i < labels.Length; i++) labels[i] = "Registrador " + (i + 1);
                    }
                    StringBuilder decoded = new StringBuilder();
                    for (int i = 0; i < values.Length; i++) decoded.AppendLine(labels[i] + ": " + values[i] + " (" + values[i].ToString("X4") + "h)");
                    output.Text = decoded.ToString();
                    return;
                }
                StringBuilder report = new StringBuilder();
                report.AppendLine("Quadros gerados: " + frames.Count);
                foreach (byte[] frame in frames) report.AppendLine(Hex(frame));
                output.Text = report.ToString();
            }
            catch (ArgumentException ex) { output.Text = ex.Message; }
            catch (FormatException ex) { output.Text = ex.Message; }
            catch (OverflowException) { output.Text = "Valor numérico fora da faixa permitida."; }
        }
    }
}

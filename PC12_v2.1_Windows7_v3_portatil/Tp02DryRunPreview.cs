using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Visualizador offline do compilador Ladder -> TP02.
    /// Nao possui SerialPort e nao transmite nenhum quadro.
    /// </summary>
    internal sealed class Tp02DryRunPreviewForm : Form
    {
        private readonly LadderEditorForm editor;
        private readonly PlcDeviceProfile profile;
        private TextBox output;
        private NumericUpDown station;
        private NumericUpDown start;
        private NumericUpDown responseCode;
        private Label state;
        private string currentReport = string.Empty;

        public Tp02DryRunPreviewForm(LadderEditorForm editor, PlcDeviceProfile profile)
        {
            this.editor = editor;
            this.profile = profile;

            Text = "Pre-compilacao TP02 - dry-run";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(940, 650);
            Size = new Size(1180, 780);
            BackColor = OpenLadderPalette.Shell;
            ForeColor = OpenLadderPalette.Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            Recompile();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 70;
            header.BackColor = OpenLadderPalette.Chrome;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "PRE-COMPILACAO TP02 - LADDER -> 24 BITS -> WBP";
            title.AutoSize = true;
            title.Location = new Point(18, 13);
            title.Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            title.ForeColor = OpenLadderPalette.Fore;
            header.Controls.Add(title);

            Label safe = new Label();
            safe.Text = "OFFLINE / DRY-RUN - NENHUM BYTE E ENVIADO AO PLC";
            safe.AutoSize = true;
            safe.Location = new Point(20, 41);
            safe.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            safe.ForeColor = OpenLadderPalette.Warning;
            header.Controls.Add(safe);

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 58;
            bar.BackColor = OpenLadderPalette.Shell;
            Controls.Add(bar);

            int x = 16;
            bar.Controls.Add(NewLabel("Estacao", x, 8));
            station = NewNumber(x, 27, 0, 99, 1); x += 92;
            bar.Controls.Add(station);

            bar.Controls.Add(NewLabel("Inicio", x, 8));
            start = NewNumber(x, 27, 0, 4000, 0); x += 92;
            bar.Controls.Add(start);

            bar.Controls.Add(NewLabel("RI", x, 8));
            responseCode = NewNumber(x, 27, 0, 15, 5); x += 76;
            bar.Controls.Add(responseCode);

            Button compile = NewButton("RECOMPILAR", x, 15, 120);
            compile.Click += delegate { Recompile(); };
            bar.Controls.Add(compile); x += 132;

            Button export = NewButton("EXPORTAR TXT", x, 15, 125);
            export.Click += delegate { ExportReport(); };
            bar.Controls.Add(export);

            state = new Label();
            state.Dock = DockStyle.Right;
            state.Width = 360;
            state.TextAlign = ContentAlignment.MiddleRight;
            state.Padding = new Padding(0, 0, 18, 0);
            state.ForeColor = OpenLadderPalette.Muted;
            bar.Controls.Add(state);

            output = new TextBox();
            output.Dock = DockStyle.Fill;
            output.Multiline = true;
            output.ReadOnly = true;
            output.WordWrap = false;
            output.ScrollBars = ScrollBars.Both;
            output.Font = new Font("Consolas", 9.3f);
            output.BackColor = OpenLadderPalette.Canvas;
            output.ForeColor = OpenLadderPalette.Fore;
            output.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(output);
            output.BringToFront();
        }

        private static Label NewLabel(string text, int left, int top)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(left, top);
            label.ForeColor = OpenLadderPalette.Muted;
            label.Font = new Font("Segoe UI", 7.8f, FontStyle.Bold);
            return label;
        }

        private static NumericUpDown NewNumber(int left, int top, int minimum, int maximum, int value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(left, top);
            n.Size = new Size(76, 24);
            n.Minimum = minimum;
            n.Maximum = maximum;
            n.Value = value;
            n.BackColor = OpenLadderPalette.Chrome;
            n.ForeColor = OpenLadderPalette.Fore;
            return n;
        }

        private static Button NewButton(string text, int left, int top, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = OpenLadderPalette.Border;
            b.BackColor = OpenLadderPalette.Chrome;
            b.ForeColor = OpenLadderPalette.Fore;
            b.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            return b;
        }

        private void Recompile()
        {
            StringBuilder report = new StringBuilder();
            try
            {
                if (editor == null || editor.IsDisposed)
                    throw new InvalidOperationException("Editor Ladder nao esta disponivel.");
                if (profile == null || !string.Equals(profile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Selecione um perfil WEG TP02 antes de usar a pre-compilacao.");

                LadderProjectDocument document = CaptureEditorDocument(editor);
                Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(document);
                report.Append(result.BuildReport());

                if (result.Success && result.Words.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("QUADROS WBP GERADOS SOMENTE PARA INSPECAO");
                    report.AppendLine(new string('-', 66));
                    List<string> frames = Tp02LadderTargetCompiler.BuildWbpDryRunFrames(
                        result,
                        Decimal.ToInt32(station.Value),
                        Decimal.ToInt32(start.Value),
                        Decimal.ToInt32(responseCode.Value));
                    int i;
                    for (i = 0; i < frames.Count; i++)
                    {
                        report.Append("WBP[");
                        report.Append(i.ToString("00"));
                        report.Append("] ");
                        report.AppendLine(frames[i].Replace("\r", "<CR>"));
                    }
                    state.Text = result.Words.Count.ToString() + " passo(s) / " + frames.Count.ToString() + " quadro(s) / offline";
                    state.ForeColor = OpenLadderPalette.Ok;
                }
                else
                {
                    state.Text = result.Errors.Count.ToString() + " erro(s) - nenhum WBP gerado";
                    state.ForeColor = OpenLadderPalette.Warning;
                }
            }
            catch (Exception ex)
            {
                report.Clear();
                report.AppendLine("PRE-COMPILACAO TP02 INTERROMPIDA");
                report.AppendLine();
                report.AppendLine(ex.Message);
                report.AppendLine();
                report.AppendLine("Nenhum byte foi transmitido ao PLC.");
                state.Text = "dry-run indisponivel";
                state.ForeColor = OpenLadderPalette.Warning;
            }

            currentReport = report.ToString();
            output.Text = currentReport;
            output.SelectionStart = 0;
            output.SelectionLength = 0;
        }

        private static LadderProjectDocument CaptureEditorDocument(LadderEditorForm source)
        {
            MethodInfo serialize = typeof(LadderEditorForm).GetMethod("SerializeProject", BindingFlags.Instance | BindingFlags.NonPublic);
            if (serialize == null) throw new MissingMethodException("LadderEditorForm.SerializeProject nao encontrado.");
            string data = serialize.Invoke(source, null) as string;
            if (string.IsNullOrEmpty(data)) throw new InvalidDataException("Editor retornou projeto vazio.");
            return LadderProjectCodec.Deserialize(data);
        }

        private void ExportReport()
        {
            if (string.IsNullOrEmpty(currentReport)) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Relatorio TP02 (*.txt)|*.txt";
            dlg.DefaultExt = "txt";
            dlg.AddExtension = true;
            dlg.FileName = "tp02_precompilacao_dryrun.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, currentReport, Encoding.UTF8);
            state.Text = "Relatorio exportado - nenhum envio ao PLC";
        }
    }
}

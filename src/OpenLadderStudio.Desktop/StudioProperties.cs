using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Cartao de propriedades do elemento selecionado no editor Ladder. Fica no
    /// rodape do painel de instrucoes e mostra tipo, operando, parametro e posicao
    /// (L e C, como na calha e no cabecalho do diagrama).
    /// </summary>
    internal sealed class SelectionPropertiesCard : Panel
    {
        private readonly Label kind = new Label();
        private readonly Label operand = new Label();
        private readonly Label parameter = new Label();
        private readonly Label position = new Label();
        private string lastKey;

        public SelectionPropertiesCard()
        {
            Dock = DockStyle.Bottom;
            Height = 156;
            BackColor = OpenLadderPalette.NavBg;

            // Mesma faixa de titulo dos outros paineis; as linhas ganham folga para respirar.
            AddRow("Elemento", kind, 42);
            AddRow("Operando", operand, 68);
            AddRow("Parâmetro", parameter, 94);
            AddRow("Posição", position, 120);

            PanelHeader title = new PanelHeader("PROPRIEDADES");
            title.Icon = StudioIcon.Gear;
            Controls.Add(title);

            ShowSelection(-1, -1, 0, null, string.Empty, string.Empty);
        }

        private void AddRow(string caption, Label value, int top)
        {
            Label key = new Label();
            key.Text = caption;
            key.AutoSize = false;
            key.Size = new Size(80, 24);
            key.Location = new Point(14, top);
            key.TextAlign = ContentAlignment.MiddleLeft;
            key.Font = new Font("Segoe UI", 9.0f);
            key.ForeColor = OpenLadderPalette.Muted;
            Controls.Add(key);

            value.AutoSize = false;
            value.Location = new Point(98, top);
            value.Size = new Size(Math.Max(60, Width - 112), 24);
            value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.AutoEllipsis = true;
            value.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            value.ForeColor = OpenLadderPalette.Fore;
            Controls.Add(value);
        }

        /// <summary>Atualiza so quando algo mudou: o shell chama a cada repintura do canvas.</summary>
        public void ShowSelection(int rung, int column, int lane, string kindText, string operandText, string parameterText)
        {
            string key = rung + "|" + column + "|" + lane + "|" + kindText + "|" + operandText + "|" + parameterText;
            if (key == lastKey) return;
            lastKey = key;

            if (rung < 0)
            {
                kind.Text = "Nenhum elemento selecionado";
                operand.Text = "—";
                parameter.Text = "—";
                position.Text = "—";
                return;
            }

            kind.Text = string.IsNullOrEmpty(kindText) ? "Vazio" : kindText;
            operand.Text = string.IsNullOrEmpty(operandText) ? "—" : operandText;
            parameter.Text = string.IsNullOrEmpty(parameterText) ? "—" : parameterText;
            position.Text = "L" + (rung + 1) + " · C" + (column + 1) + (lane == 1 ? " · ramo paralelo" : string.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen line = new Pen(OpenLadderPalette.Border))
                e.Graphics.DrawLine(line, 0, 0, Width, 0);
        }
    }
}

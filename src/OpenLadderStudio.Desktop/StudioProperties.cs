using System;
using System.Drawing;
using System.Windows.Forms;
using OpenLadderStudio.Core;

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

        // Comentario da linha selecionada: editavel aqui, com o mesmo contrato do diagrama
        // (Ctrl+Enter ou sair do campo confirma, Esc volta ao texto gravado).
        private readonly Panel commentFrame = new Panel();
        private readonly TextBox commentBox = new TextBox();
        private int boundRung = -1;
        private string boundComment = string.Empty;

        /// <summary>Pedido de gravar o comentario (linha 0-based, texto novo).</summary>
        public event Action<int, string> CommentCommitted;

        public SelectionPropertiesCard()
        {
            Dock = DockStyle.Bottom;
            Height = 232;
            BackColor = OpenLadderPalette.NavBg;

            // Mesma faixa de titulo dos outros paineis; as linhas ganham folga para respirar.
            AddRow("Elemento", kind, 40);
            AddRow("Operando", operand, 63);
            AddRow("Parâmetro", parameter, 86);
            AddRow("Posição", position, 109);
            BuildCommentEditor(140);

            PanelHeader title = new PanelHeader("PROPRIEDADES");
            title.Icon = StudioIcon.Gear;
            Controls.Add(title);

            ShowSelection(-1, -1, 0, null, string.Empty, string.Empty);
        }

        private void BuildCommentEditor(int top)
        {
            Label caption = new Label();
            caption.Text = "Comentário da linha";
            caption.AutoSize = false;
            caption.Size = new Size(200, 22);
            caption.Location = new Point(14, top);
            caption.TextAlign = ContentAlignment.MiddleLeft;
            caption.Font = new Font("Segoe UI", 9.0f);
            caption.ForeColor = OpenLadderPalette.Muted;
            Controls.Add(caption);

            commentFrame.Location = new Point(14, top + 24);
            commentFrame.Size = new Size(Math.Max(100, Width - 28), 62);
            commentFrame.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            commentFrame.BackColor = OpenLadderPalette.ChromeLight;
            commentFrame.Padding = new Padding(8, 6, 8, 6);
            commentFrame.Paint += delegate(object sender, PaintEventArgs e)
            {
                bool focused = commentBox.Focused;
                using (Pen pen = new Pen(focused ? OpenLadderPalette.Focus : OpenLadderPalette.Border, focused ? 2.0f : 1.0f))
                    e.Graphics.DrawRectangle(pen, 0, 0, commentFrame.Width - 1, commentFrame.Height - 1);
            };

            commentBox.Multiline = true;
            commentBox.AcceptsReturn = true;
            commentBox.WordWrap = true;
            commentBox.BorderStyle = BorderStyle.None;
            commentBox.MaxLength = LadderProjectCodec.MaxCommentLength;
            commentBox.Dock = DockStyle.Fill;
            commentBox.BackColor = OpenLadderPalette.ChromeLight;
            commentBox.ForeColor = OpenLadderPalette.Fore;
            commentBox.Font = new Font("Segoe UI", 9.0f);
            commentBox.AccessibleName = "Comentário da linha selecionada";
            commentBox.GotFocus += delegate { commentFrame.Invalidate(); };
            commentBox.LostFocus += delegate { commentFrame.Invalidate(); CommitComment(); };
            commentBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    CommitComment();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    commentBox.Text = ToBoxText(boundComment);
                    commentBox.SelectionStart = commentBox.TextLength;
                }
            };
            commentFrame.Controls.Add(commentBox);
            Controls.Add(commentFrame);
        }

        private static string ToBoxText(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private void CommitComment()
        {
            if (boundRung < 0) return;
            string text = commentBox.Text.Replace("\r\n", "\n");
            if (string.Equals(text, boundComment.Replace("\r\n", "\n"), StringComparison.Ordinal)) return;
            int rung = boundRung;
            // O que fica ligado e o que o projeto vai gravar, nao o texto cru digitado.
            string stored = LadderProjectCodec.NormalizeComment(text);
            boundComment = stored;
            string shown = ToBoxText(stored);
            if (!string.Equals(commentBox.Text, shown, StringComparison.Ordinal)) commentBox.Text = shown;
            if (CommentCommitted != null) CommentCommitted(rung, stored);
        }

        /// <summary>
        /// Confirma o texto digitado e ainda nao confirmado. O shell chama antes de salvar,
        /// criar ou abrir projeto, para que o texto entre no documento em que foi escrito.
        /// </summary>
        public void CommitPendingComment()
        {
            CommitComment();
        }

        /// <summary>
        /// Liga o campo de comentario a linha selecionada. Nao reescreve o texto enquanto o
        /// campo tem o foco: o canvas repinta o tempo todo e isso apagaria o que se digita.
        /// </summary>
        public void ShowRung(int rung, string comment)
        {
            comment = comment ?? string.Empty;
            bool enabled = rung >= 0;
            if (commentBox.Enabled != enabled) commentBox.Enabled = enabled;
            if (commentBox.Focused && rung == boundRung) return;

            // Linha diferente com o campo em foco: religa SEM confirmar (descarta o pendente).
            // Depende da invariante: toda troca de SelectedRung ou tira o foco do campo antes
            // (LostFocus confirma na linha certa) ou passa por InvokeLadder, que chama
            // CommitPendingComment. Quem criar outro caminho de troca precisa respeita-la.
            if (rung == boundRung && comment == boundComment) return;
            boundRung = rung;
            boundComment = comment;
            commentBox.Text = ToBoxText(comment);
        }

        private void AddRow(string caption, Label value, int top)
        {
            Label key = new Label();
            key.Text = caption;
            key.AutoSize = false;
            key.Size = new Size(80, 22);
            key.Location = new Point(14, top);
            key.TextAlign = ContentAlignment.MiddleLeft;
            key.Font = new Font("Segoe UI", 9.0f);
            key.ForeColor = OpenLadderPalette.Muted;
            Controls.Add(key);

            value.AutoSize = false;
            value.Location = new Point(98, top);
            value.Size = new Size(Math.Max(60, Width - 112), 22);
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

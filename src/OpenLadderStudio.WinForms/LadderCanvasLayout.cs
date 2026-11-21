using System;
using System.Collections.Generic;
using System.Drawing;

namespace ModernPC12
{
    /// <summary>Região do canvas sob um ponto, na ordem em que o ponteiro as encontra.</summary>
    internal enum LadderRegion
    {
        None,
        Gutter,
        Comment,
        Cell,
        AddRow
    }

    /// <summary>
    /// Geometria do diagrama em unidades lógicas (antes do zoom e do DPI). Não conhece
    /// controle, linha nem elemento: recebe o comentário de cada linha e se ela tem ramo
    /// paralelo, e devolve onde fica cada coisa. O canvas pinta e testa o ponteiro
    /// com os mesmos números, e o autoteste confere a conta sem abrir janela.
    ///
    /// Cada linha é um bloco: faixa de comentário em cima (altura pelo texto), diagrama
    /// embaixo (mais alto só quando há ramo paralelo). Depois da última linha vem a
    /// linha-fantasma "+ Nova linha".
    /// </summary>
    internal sealed class LadderCanvasLayout
    {
        public const int ColumnCount = 8;
        // Largura de projeto: abaixo dela o diagrama encolhe (ate MinFit) e depois rola. Mais estreita que
        // antes (960) para o texto continuar legivel em janela de 1280 px com os paineis laterais abertos.
        public const int DesignWidth = 720;
        public const float MinFit = 0.8f;
        public const int HeaderHeight = 28;
        public const int TopPad = 10;
        public const int GutterWidth = 60;
        public const int LeftRail = 80;
        public const int RightPad = 28;

        public const int CommentFontPixels = 13;
        public const int CommentPadX = 12;
        public const int CommentPadY = 8;
        public const int CommentMaxLines = 5;

        public const int WireY = 58;
        public const int BranchOffset = 64;
        public const int DiagramHeightPlain = 108;
        public const int DiagramHeightBranch = 176;
        public const int AddRowHeight = 64;
        public const int BottomPad = 48;

        /// <summary>Os símbolos de LadderSymbols foram desenhados para 62 px de célula; aqui crescem para ler bem.</summary>
        public const float SymbolScale = 1.3f;

        public int Count;
        public int Width;
        public int RightRail;
        public int CellWidth;
        public int[] Top;
        public int[] CommentHeight;
        public int[] DiagramTop;
        public int[] Height;
        public bool[] Branch;
        public int AddTop;
        public int ContentBottom;
        public int ContentHeight;

        public int CommentWidth { get { return Math.Max(40, RightRail - LeftRail); } }

        public int WireAt(int rung) { return DiagramTop[rung] + WireY; }

        public int BranchAt(int rung) { return WireAt(rung) + BranchOffset; }

        /// <summary>Faixa de comentário da linha, alinhada à área do diagrama.</summary>
        public Rectangle CommentRect(int rung)
        {
            return new Rectangle(LeftRail, Top[rung], CommentWidth, CommentHeight[rung]);
        }

        /// <summary>Caixa de seleção da célula principal (fio) de uma coluna.</summary>
        public Rectangle CellRect(int rung, int column)
        {
            return new Rectangle(LeftRail + column * CellWidth + 3, WireAt(rung) - 38, CellWidth - 6, 76);
        }

        /// <summary>Caixa de seleção do elemento do ramo paralelo.</summary>
        public Rectangle BranchRect(int rung, int column)
        {
            return new Rectangle(LeftRail + column * CellWidth + 3, BranchAt(rung) - 30, CellWidth - 6, 62);
        }

        public Rectangle AddRect()
        {
            return new Rectangle(LeftRail, AddTop + 10, CommentWidth, AddRowHeight - 20);
        }

        public static LadderCanvasLayout Compute(string[] comments, bool[] hasBranch, int width)
        {
            int count = comments == null ? 0 : comments.Length;
            LadderCanvasLayout l = new LadderCanvasLayout();
            l.Count = count;
            l.Width = Math.Max(DesignWidth, width);
            l.RightRail = l.Width - RightPad;
            l.CellWidth = Math.Max(1, (l.RightRail - LeftRail) / ColumnCount);
            l.Top = new int[count];
            l.CommentHeight = new int[count];
            l.DiagramTop = new int[count];
            l.Height = new int[count];
            l.Branch = new bool[count];

            int textWidth = l.CommentWidth - 2 * CommentPadX - 6;
            int y = HeaderHeight + TopPad;
            for (int r = 0; r < count; r++)
            {
                bool truncated;
                int lines = CommentLines(comments[r], textWidth, out truncated);
                l.Top[r] = y;
                l.CommentHeight[r] = CommentHeightFor(lines);
                l.DiagramTop[r] = y + l.CommentHeight[r];
                l.Branch[r] = hasBranch != null && r < hasBranch.Length && hasBranch[r];
                l.Height[r] = l.CommentHeight[r] + (l.Branch[r] ? DiagramHeightBranch : DiagramHeightPlain);
                y += l.Height[r];
            }

            l.AddTop = y;
            l.ContentBottom = y + AddRowHeight;
            l.ContentHeight = l.ContentBottom + BottomPad;
            return l;
        }

        public static int CommentHeightFor(int lines)
        {
            return 2 * CommentPadY + Math.Max(1, lines) * LineHeight();
        }

        public static int LineHeight()
        {
            return 18;
        }

        /// <summary>
        /// Descobre o que está sob o ponto (em unidades lógicas). Para células, informa a
        /// coluna e a faixa: 1 quando o ponto está sobre o ramo paralelo de uma coluna que o tem.
        /// </summary>
        public LadderRegion Locate(int px, int py, Func<int, int, bool> hasParallel, out int rung, out int column, out int lane)
        {
            rung = -1;
            column = -1;
            lane = 0;

            if (py >= AddTop && py < AddTop + AddRowHeight)
            {
                bool inside = px >= GutterWidth && px <= RightRail;
                return inside ? LadderRegion.AddRow : LadderRegion.None;
            }

            for (int r = 0; r < Count; r++)
            {
                if (py < Top[r] || py >= Top[r] + Height[r]) continue;
                rung = r;

                if (px < LeftRail) return px >= 0 ? LadderRegion.Gutter : LadderRegion.None;
                if (px > RightRail) return LadderRegion.None;
                if (py < DiagramTop[r]) return LadderRegion.Comment;

                int c = Math.Min(ColumnCount - 1, (px - LeftRail) / CellWidth);
                column = c;
                bool branchHere = Branch[r] && hasParallel != null && hasParallel(r, c);
                lane = branchHere && Math.Abs(py - BranchAt(r)) <= 28 ? 1 : 0;
                return LadderRegion.Cell;
            }
            return LadderRegion.None;
        }

        // ------------------------------------------------------------------
        // Medida do comentário. O desenho usa a mesma fonte e o mesmo formato,
        // então a quebra de linha da conta é a que aparece na tela.
        // ------------------------------------------------------------------

        private static readonly Dictionary<string, int> lineCache = new Dictionary<string, int>();
        private static Bitmap measureBitmap;
        private static Graphics measureGraphics;

        public static StringFormat CommentFormat()
        {
            StringFormat format = new StringFormat(StringFormat.GenericTypographic);
            format.FormatFlags &= ~StringFormatFlags.NoWrap;
            format.FormatFlags |= StringFormatFlags.LineLimit;
            format.Trimming = StringTrimming.EllipsisWord;
            return format;
        }

        public static Font CommentFont()
        {
            return new Font("Segoe UI", CommentFontPixels, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        /// <summary>Linhas que o texto ocupa na largura dada, de 1 a CommentMaxLines; truncated diz se passou do limite.</summary>
        public static int CommentLines(string text, int widthPixels, out bool truncated)
        {
            truncated = false;
            if (string.IsNullOrEmpty(text)) return 1;

            int width = Math.Max(20, widthPixels);
            string key = width.ToString() + "|" + text;
            int cached;
            int raw;
            if (lineCache.TryGetValue(key, out cached)) raw = cached;
            else
            {
                raw = MeasureLines(text, width);
                if (lineCache.Count > 512) lineCache.Clear();
                lineCache[key] = raw;
            }

            truncated = raw > CommentMaxLines;
            return Math.Max(1, Math.Min(CommentMaxLines, raw));
        }

        private static int MeasureLines(string text, int width)
        {
            try
            {
                if (measureGraphics == null)
                {
                    measureBitmap = new Bitmap(1, 1);
                    measureGraphics = Graphics.FromImage(measureBitmap);
                    measureGraphics.PageUnit = GraphicsUnit.Pixel;
                }
                using (Font font = CommentFont())
                using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
                {
                    format.FormatFlags &= ~StringFormatFlags.NoWrap;
                    SizeF size = measureGraphics.MeasureString(text, font, width, format);
                    float line = Math.Max(1.0f, font.GetHeight(measureGraphics));
                    return Math.Max(1, (int)Math.Ceiling((size.Height - 0.5f) / line));
                }
            }
            catch
            {
                // Sem GDI+ utilizável, conta só as quebras explícitas.
                return Math.Max(1, text.Split('\n').Length);
            }
        }
    }
}

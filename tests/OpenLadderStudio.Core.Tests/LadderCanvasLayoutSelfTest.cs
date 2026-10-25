using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using ModernPC12;

namespace OpenLadderStudio.Core.Tests
{
    /// <summary>
    /// Autoteste da geometria do canvas Ladder (faixa de comentário, blocos de linha,
    /// linha-fantasma e localização do ponteiro). Não abre janela: só conta.
    /// </summary>
    internal static class LadderCanvasLayoutSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Console.WriteLine("Autoteste da geometria do canvas Ladder");
            Console.WriteLine();

            TestCommentLines();
            TestBlocksStackWithoutGaps();
            TestWidthClamp();
            TestLocateRoundTrip();
            TestBranchLane();
            TestOutsideRegions();
            TestEmptyProject();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificações passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificação(ões) falharam.");
            return 1;
        }

        private static void Check(bool condition, string description)
        {
            if (condition)
            {
                Console.WriteLine("  ok   " + description);
                return;
            }
            failures++;
            Console.WriteLine("  FALHA " + description);
        }

        private static string Repeat(string word, int times)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < times; i++) b.Append(word).Append(' ');
            return b.ToString().Trim();
        }

        private static void TestCommentLines()
        {
            Console.WriteLine("Comentário: linhas ocupadas");
            bool truncated;
            Check(LadderCanvasLayout.CommentLines(null, 600, out truncated) == 1 && !truncated, "sem comentário ocupa uma linha");
            Check(LadderCanvasLayout.CommentLines(string.Empty, 600, out truncated) == 1, "comentário vazio ocupa uma linha");
            Check(LadderCanvasLayout.CommentLines("Parte de partida do motor", 600, out truncated) == 1 && !truncated, "frase curta cabe numa linha");
            Check(LadderCanvasLayout.CommentLines("a\nb\nc", 600, out truncated) == 3, "três quebras explícitas ocupam três linhas");

            string longText = Repeat("intertravamento", 120);
            int wide = LadderCanvasLayout.CommentLines(longText, 800, out truncated);
            Check(wide == LadderCanvasLayout.CommentMaxLines && truncated, "texto longo para no limite e avisa que cortou");

            string medium = Repeat("partida", 18);
            bool t1, t2;
            int narrow = LadderCanvasLayout.CommentLines(medium, 200, out t1);
            int roomy = LadderCanvasLayout.CommentLines(medium, 700, out t2);
            Check(narrow > roomy, "a mesma frase ocupa mais linhas numa faixa estreita");
            Check(LadderCanvasLayout.CommentHeightFor(3) > LadderCanvasLayout.CommentHeightFor(1), "a altura da faixa cresce com as linhas");
            Check(LadderCanvasLayout.CommentHeightFor(0) == LadderCanvasLayout.CommentHeightFor(1), "altura mínima é a de uma linha");
        }

        private static LadderCanvasLayout Sample(out string[] comments, out bool[] branch)
        {
            comments = new string[] { string.Empty, "Habilita a partida", "linha 1\nlinha 2\nlinha 3" };
            branch = new bool[] { false, true, false };
            return LadderCanvasLayout.Compute(comments, branch, 1200);
        }

        private static void TestBlocksStackWithoutGaps()
        {
            Console.WriteLine("Blocos de linha");
            string[] comments;
            bool[] branch;
            LadderCanvasLayout l = Sample(out comments, out branch);

            Check(l.Count == 3, "uma entrada de layout por linha");
            Check(l.Top[0] == LadderCanvasLayout.HeaderHeight + LadderCanvasLayout.TopPad, "a primeira linha começa logo abaixo do cabeçalho");
            bool stacked = true;
            for (int r = 0; r + 1 < l.Count; r++) stacked &= l.Top[r + 1] == l.Top[r] + l.Height[r];
            Check(stacked, "cada linha começa onde a anterior termina, sem folga nem sobreposição");
            Check(l.AddTop == l.Top[2] + l.Height[2], "a linha-fantasma vem logo depois da última linha");
            Check(l.ContentBottom == l.AddTop + LadderCanvasLayout.AddRowHeight, "o conteúdo termina no fim da linha-fantasma");
            Check(l.ContentHeight > l.ContentBottom, "há margem de rolagem abaixo do conteúdo");

            Check(l.CommentHeight[0] == LadderCanvasLayout.CommentHeightFor(1), "comentário vazio usa a faixa de uma linha");
            Check(l.CommentHeight[2] == LadderCanvasLayout.CommentHeightFor(3), "três linhas de comentário dão a faixa de três linhas");
            Check(l.Height[0] == l.CommentHeight[0] + LadderCanvasLayout.DiagramHeightPlain, "linha sem ramo usa o diagrama baixo");
            Check(l.Height[1] == l.CommentHeight[1] + LadderCanvasLayout.DiagramHeightBranch, "linha com ramo usa o diagrama alto");
            Check(l.DiagramTop[1] == l.CommentRect(1).Bottom, "o diagrama começa onde a faixa de comentário acaba");
            Check(l.WireAt(0) == l.DiagramTop[0] + LadderCanvasLayout.WireY, "o fio fica a distância fixa do topo do diagrama");

            Rectangle c0 = l.CellRect(0, 0);
            Rectangle c1 = l.CellRect(0, 1);
            Check(c0.Right <= c1.Left, "células vizinhas não se sobrepõem");
            Check(c0.Top >= l.DiagramTop[0] && c0.Bottom <= l.Top[0] + l.Height[0], "a célula cabe dentro do diagrama da própria linha");
            Rectangle b = l.BranchRect(1, 3);
            Check(b.Top >= l.DiagramTop[1] && b.Bottom <= l.Top[1] + l.Height[1], "o ramo paralelo cabe dentro da linha que o tem");
            Check(l.CommentRect(1).Left == LadderCanvasLayout.LeftRail, "a faixa de comentário se alinha ao início do diagrama");
            Check(l.CommentRect(1).Right == l.RightRail, "a faixa de comentário vai até o trilho direito");
        }

        private static void TestWidthClamp()
        {
            Console.WriteLine("Largura");
            string[] comments = new string[] { string.Empty };
            LadderCanvasLayout narrow = LadderCanvasLayout.Compute(comments, new bool[] { false }, 300);
            Check(narrow.Width == LadderCanvasLayout.DesignWidth, "janela estreita usa a largura de projeto");
            LadderCanvasLayout wide = LadderCanvasLayout.Compute(comments, new bool[] { false }, 1600);
            Check(wide.Width == 1600 && wide.RightRail == 1600 - LadderCanvasLayout.RightPad, "janela larga alarga o diagrama");
            int span = wide.RightRail - LadderCanvasLayout.LeftRail;
            Check(wide.CellWidth * LadderCanvasLayout.ColumnCount <= span && span - wide.CellWidth * LadderCanvasLayout.ColumnCount < LadderCanvasLayout.ColumnCount,
                "as oito colunas ocupam a largura do diagrama (resto menor que uma coluna por pixel)");
            Check(wide.CellWidth > narrow.CellWidth, "coluna mais larga em janela larga");
        }

        private static void TestLocateRoundTrip()
        {
            Console.WriteLine("Ponteiro: do retângulo à região");
            string[] comments;
            bool[] branch;
            LadderCanvasLayout l = Sample(out comments, out branch);
            Func<int, int, bool> none = delegate(int r, int c) { return false; };

            bool allComments = true;
            bool allCells = true;
            bool allGutters = true;
            for (int r = 0; r < l.Count; r++)
            {
                int rung, col, lane;
                Rectangle cm = l.CommentRect(r);
                allComments &= l.Locate(cm.Left + cm.Width / 2, cm.Top + cm.Height / 2, none, out rung, out col, out lane) == LadderRegion.Comment && rung == r;

                for (int c = 0; c < LadderCanvasLayout.ColumnCount; c++)
                {
                    Rectangle cell = l.CellRect(r, c);
                    LadderRegion region = l.Locate(cell.Left + cell.Width / 2, l.WireAt(r), none, out rung, out col, out lane);
                    allCells &= region == LadderRegion.Cell && rung == r && col == c && lane == 0;
                }

                allGutters &= l.Locate(20, l.WireAt(r), none, out rung, out col, out lane) == LadderRegion.Gutter && rung == r;
            }
            Check(allComments, "o centro de cada faixa de comentário é Comentário daquela linha");
            Check(allCells, "o centro de cada célula é a célula certa (linha e coluna)");
            Check(allGutters, "a calha à esquerda é Calha da linha");

            int rr, cc, ll;
            Rectangle add = l.AddRect();
            Check(l.Locate(add.Left + add.Width / 2, add.Top + add.Height / 2, none, out rr, out cc, out ll) == LadderRegion.AddRow, "a linha-fantasma é a ação de nova linha");
            Check(rr == -1, "a linha-fantasma não pertence a nenhuma linha");

            LadderCanvasLayout one = LadderCanvasLayout.Compute(new string[] { string.Empty }, new bool[] { false }, 960);
            Rectangle last = one.CellRect(0, LadderCanvasLayout.ColumnCount - 1);
            Check(one.Locate(one.RightRail - 1, one.WireAt(0), none, out rr, out cc, out ll) == LadderRegion.Cell && cc == LadderCanvasLayout.ColumnCount - 1,
                "o último pixel antes do trilho direito ainda é a coluna de saída");
            Check(last.Right <= one.RightRail, "a caixa da coluna de saída não passa do trilho");
        }

        private static void TestBranchLane()
        {
            Console.WriteLine("Ramo paralelo");
            string[] comments;
            bool[] branch;
            LadderCanvasLayout l = Sample(out comments, out branch);
            Func<int, int, bool> branchAtThree = delegate(int r, int c) { return r == 1 && c == 3; };
            Func<int, int, bool> none = delegate(int r, int c) { return false; };

            int rung, col, lane;
            Rectangle b = l.BranchRect(1, 3);
            LadderRegion region = l.Locate(b.Left + b.Width / 2, l.BranchAt(1), branchAtThree, out rung, out col, out lane);
            Check(region == LadderRegion.Cell && rung == 1 && col == 3 && lane == 1, "sobre o ramo de uma coluna que o tem, a faixa é a do ramo");

            region = l.Locate(b.Left + b.Width / 2, l.BranchAt(1), none, out rung, out col, out lane);
            Check(region == LadderRegion.Cell && lane == 0, "sem ramo na coluna, o mesmo ponto cai na faixa principal");

            region = l.Locate(b.Left + b.Width / 2, l.WireAt(1), branchAtThree, out rung, out col, out lane);
            Check(region == LadderRegion.Cell && lane == 0, "sobre o fio principal a faixa é a principal mesmo com ramo");

            region = l.Locate(l.CellRect(0, 3).Left + 10, l.WireAt(0) + 60, branchAtThree, out rung, out col, out lane);
            Check(lane == 0, "linha sem ramo nunca devolve a faixa do ramo");
        }

        private static void TestOutsideRegions()
        {
            Console.WriteLine("Fora das regiões");
            string[] comments;
            bool[] branch;
            LadderCanvasLayout l = Sample(out comments, out branch);
            Func<int, int, bool> none = delegate(int r, int c) { return false; };
            int rung, col, lane;

            Check(l.Locate(300, 5, none, out rung, out col, out lane) == LadderRegion.None && rung == -1, "acima da primeira linha não há nada");
            Check(l.Locate(300, l.ContentHeight + 200, none, out rung, out col, out lane) == LadderRegion.None, "abaixo do conteúdo não há nada");
            Check(l.Locate(l.RightRail + 5, l.WireAt(0), none, out rung, out col, out lane) == LadderRegion.None, "à direita do trilho não há nada");
            Check(l.Locate(-4, l.WireAt(0), none, out rung, out col, out lane) == LadderRegion.None, "à esquerda da calha não há nada");
            Check(l.Locate(LadderCanvasLayout.LeftRail - 1, l.CommentRect(0).Top + 4, none, out rung, out col, out lane) == LadderRegion.Gutter,
                "antes do início da faixa de comentário ainda é a calha");
        }

        private static void TestEmptyProject()
        {
            Console.WriteLine("Projeto sem linhas");
            LadderCanvasLayout l = LadderCanvasLayout.Compute(new string[0], new bool[0], 1000);
            Check(l.Count == 0, "zero linhas, zero blocos");
            Check(l.AddTop == LadderCanvasLayout.HeaderHeight + LadderCanvasLayout.TopPad, "a linha-fantasma sobe para o topo");
            int rung, col, lane;
            Rectangle add = l.AddRect();
            Check(l.Locate(add.Left + 5, add.Top + 5, null, out rung, out col, out lane) == LadderRegion.AddRow, "com o projeto vazio ainda dá para adicionar linha");
            LadderCanvasLayout fromNull = LadderCanvasLayout.Compute(null, null, 1000);
            Check(fromNull.Count == 0, "entrada nula vira layout vazio");
        }
    }
}

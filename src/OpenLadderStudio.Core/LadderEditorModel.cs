using System;

namespace ModernPC12
{
    // Modelo do editor Ladder legado, desenhado pelo LadderCanvas compartilhado. Extraído sem
    // mudança de LadderEditor.cs para que a trilha moderna use o mesmo canvas. O formato de
    // arquivo continua sendo o do LadderProjectCodec; este é só o modelo de tela.
    // C# 5: este arquivo também é compilado pelo csc do .NET Framework.

    internal enum LadderElementType
    {
        Empty,
        ContactNO,
        ContactNC,
        Coil,
        Timer,
        Counter,
        Set,
        Reset,
        EdgeUp,
        EdgeDown,
        Function,
        End
    }

    internal sealed class LadderElement
    {
        public LadderElementType Type;
        public string Address;
        public string Parameter;
        public string Mode;

        public LadderElement()
        {
            Type = LadderElementType.Empty;
            Address = string.Empty;
            Parameter = string.Empty;
            Mode = string.Empty;
        }

        public void Clear()
        {
            Type = LadderElementType.Empty;
            Address = string.Empty;
            Parameter = string.Empty;
            Mode = string.Empty;
        }
    }

    internal sealed class LadderRung
    {
        public const int ColumnCount = 8;
        public LadderElement[] Elements;
        public LadderElement[] Parallel;
        private string comment = string.Empty;

        // Comentário da linha: documentação do projeto, nunca lógica. Viaja com o rung
        // no arquivo e no desfazer/refazer, mas nenhum caminho até o PLC o lê.
        public string Comment
        {
            get { return comment; }
            set { comment = value ?? string.Empty; }
        }

        public LadderRung()
        {
            Elements = new LadderElement[ColumnCount];
            Parallel = new LadderElement[ColumnCount];
            int i;
            for (i = 0; i < ColumnCount; i++)
            {
                Elements[i] = new LadderElement();
                Parallel[i] = new LadderElement();
            }
        }
    }
}

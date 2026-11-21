using System.Collections.Generic;

namespace ModernPC12
{
    // Modelo Ladder universal, independente de fabricante. Extraído de PLCPlatform.cs para o
    // núcleo porque o motor de varredura (LadderSimulation.cs) depende dele e é compilado
    // também pela solução moderna. O namespace foi mantido para não tocar o código legado.
    internal enum UniversalElementKind
    {
        ContactNO,
        ContactNC,
        Coil,
        Set,
        Reset,
        Timer,
        Counter,
        RisingEdge,
        FallingEdge,
        Function,
        End
    }

    internal sealed class UniversalLadderElement
    {
        public UniversalElementKind Kind = UniversalElementKind.ContactNO;
        public string Address;
        public string Parameter;
        public string FunctionCode;

        /// <summary>Coluna de origem no rung. Preserva a posição das condições e dos ramos paralelos.</summary>
        public int Column = 0;

        public UniversalLadderElement()
        {
            Address = string.Empty;
            Parameter = string.Empty;
            FunctionCode = string.Empty;
        }
    }

    internal sealed class UniversalLadderRung
    {
        public readonly List<UniversalLadderElement> Series = new List<UniversalLadderElement>();
        public readonly List<UniversalLadderElement> Parallel = new List<UniversalLadderElement>();
    }

    internal sealed class UniversalLadderProgram
    {
        public string Name;
        public readonly List<UniversalLadderRung> Rungs = new List<UniversalLadderRung>();

        public UniversalLadderProgram()
        {
            Name = "Sem nome";
        }
    }
}

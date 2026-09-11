using System;
using System.Collections.Generic;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Um registro de código de máquina tal como o caminho Write PLC Program do
    /// PC12 o acumula antes de montar um quadro PG33.
    ///
    /// StepSpan é o avanço do cursor real de programa (+0x76) associado ao
    /// registro. O código original do PC12 foi confirmado offline no Unicorn
    /// aceitando spans 1..4 e incrementando o contador de registros apenas uma
    /// vez, independentemente do span.
    /// </summary>
    internal struct Tp02Pg33DryRunRecord
    {
        public readonly Tp02MachineWord Word;
        public readonly int StepSpan;

        public Tp02Pg33DryRunRecord(Tp02MachineWord word, int stepSpan)
        {
            if (stepSpan < 1 || stepSpan > 4)
                throw new ArgumentOutOfRangeException("stepSpan", "O PC12 usa spans de 1 a 4 passos por registro.");
            Word = word;
            StepSpan = stepSpan;
        }
    }

    /// <summary>
    /// Resultado de um bloco PG33 construído somente em memória.
    /// </summary>
    internal sealed class Tp02Pg33DryRunBlock
    {
        public readonly int StartStep;
        public readonly int NextStep;
        public readonly int RecordCount;
        public readonly byte[] Frame;

        public Tp02Pg33DryRunBlock(int startStep, int nextStep, int recordCount, byte[] frame)
        {
            StartStep = startStep;
            NextStep = nextStep;
            RecordCount = recordCount;
            Frame = frame;
        }
    }

    /// <summary>
    /// Orquestrador OFFLINE dos blocos PG33.
    ///
    /// Evidência confirmada no código original do PC12 por análise estática e
    /// emulação Unicorn, sem I/O:
    /// - até 20 registros por quadro;
    /// - cada registro contém HIGH/LOW/EXTERNAL;
    /// - o cursor de programa avança 1..4 passos por registro;
    /// - depois de um quadro aceito, o bloco seguinte começa no cursor real
    ///   resultante, e não em startStep + quantidadeDeRegistros.
    ///
    /// Esta classe não abre COM, não transmite e não é ligada a qualquer botão
    /// de download. Serve exclusivamente para dry-run, comparação e testes.
    /// </summary>
    internal static class Tp02Pg33DryRunProgram
    {
        internal const int MaxProgramSteps = 4000;
        internal const int MaxRecordsPerFrame = Tp02Pg33DryRunFrame.MaxRecordsPerFrame;

        internal static IList<Tp02Pg33DryRunBlock> BuildBlocks(
            int startStep,
            IList<Tp02Pg33DryRunRecord> records)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");
            if (records == null)
                throw new ArgumentNullException("records");
            if (records.Count == 0)
                throw new ArgumentException("É necessário pelo menos um registro PG33.", "records");

            List<Tp02Pg33DryRunBlock> blocks = new List<Tp02Pg33DryRunBlock>();
            int cursor = startStep;
            int index = 0;

            while (index < records.Count)
            {
                if (cursor >= MaxProgramSteps)
                    throw new ArgumentOutOfRangeException("records", "Há registros restantes depois do limite de 4000 passos.");

                int blockStart = cursor;
                int count = Math.Min(MaxRecordsPerFrame, records.Count - index);
                byte[] highLow = new byte[count * 2];
                byte[] external = new byte[count];

                for (int local = 0; local < count; local++)
                {
                    Tp02Pg33DryRunRecord record = records[index + local];
                    if (record.StepSpan < 1 || record.StepSpan > 4)
                        throw new ArgumentOutOfRangeException("records", "Registro contém StepSpan fora de 1..4.");
                    if (cursor + record.StepSpan > MaxProgramSteps)
                        throw new ArgumentOutOfRangeException("records", "Registro ultrapassa o limite de 4000 passos do TP02-40/60.");

                    highLow[2 * local] = record.Word.High;
                    highLow[(2 * local) + 1] = record.Word.Low;
                    external[local] = record.Word.External;
                    cursor += record.StepSpan;
                }

                byte[] frame = Tp02Pg33DryRunFrame.BuildCandidate(blockStart, highLow, external);
                blocks.Add(new Tp02Pg33DryRunBlock(blockStart, cursor, count, frame));
                index += count;
            }

            return blocks;
        }
    }
}

using System;
using System.Collections.Generic;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Uma instrução lógica do caminho Write PLC Program do PC12, já expandida
    /// para as 1..4 palavras de máquina HIGH/LOW/EXTERNAL que o PG33 transporta.
    ///
    /// A análise estática do helper 0x004BCA65 mostrou que a primeira palavra é
    /// emitida pelo chamador e uma palavra adicional é emitida pelo helper para
    /// cada passo restante. Assim, o span real da instrução coincide com a
    /// quantidade de palavras de máquina desta estrutura.
    /// </summary>
    internal sealed class Tp02Pg33DryRunInstruction
    {
        public readonly Tp02MachineWord[] Words;

        public Tp02Pg33DryRunInstruction(params Tp02MachineWord[] words)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (words.Length < 1 || words.Length > 4)
                throw new ArgumentOutOfRangeException("words", "Uma instrução PG33 deve ocupar de 1 a 4 passos/palavras.");

            Words = new Tp02MachineWord[words.Length];
            Array.Copy(words, Words, words.Length);
        }

        public int StepSpan
        {
            get { return Words.Length; }
        }
    }

    /// <summary>
    /// Resultado de um bloco PG33 construído somente em memória.
    /// </summary>
    internal sealed class Tp02Pg33DryRunBlock
    {
        public readonly int StartStep;
        public readonly int NextStep;
        public readonly int InstructionCount;
        public readonly int MachineWordCount;
        public readonly byte[] Frame;

        public Tp02Pg33DryRunBlock(
            int startStep,
            int nextStep,
            int instructionCount,
            int machineWordCount,
            byte[] frame)
        {
            StartStep = startStep;
            NextStep = nextStep;
            InstructionCount = instructionCount;
            MachineWordCount = machineWordCount;
            Frame = frame;
        }
    }

    /// <summary>
    /// Orquestrador OFFLINE dos blocos PG33.
    ///
    /// Evidência confirmada no código original do PC12 por análise estática e
    /// emulação Unicorn, sem I/O:
    /// - +0x7A limita cada bloco a 20 INSTRUÇÕES LÓGICAS;
    /// - cada instrução ocupa 1..4 passos e produz 1..4 palavras de máquina;
    /// - o helper 0x004BCA65 acrescenta as palavras posteriores à primeira;
    /// - o quadro bruto usa a quantidade total W de palavras, até 80 por bloco;
    /// - depois de um quadro aceito, o bloco seguinte começa no cursor real de
    ///   passos, não em startStep + quantidade de instruções.
    ///
    /// Esta classe não abre COM, não transmite e não é ligada a qualquer botão
    /// de download. Serve exclusivamente para dry-run, comparação e testes.
    /// </summary>
    internal static class Tp02Pg33DryRunProgram
    {
        internal const int MaxProgramSteps = 4000;
        internal const int MaxInstructionsPerFrame = 20;
        internal const int MaxMachineWordsPerFrame = Tp02Pg33DryRunFrame.MaxMachineWordsPerFrame;

        internal static IList<Tp02Pg33DryRunBlock> BuildBlocks(
            int startStep,
            IList<Tp02Pg33DryRunInstruction> instructions)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");
            if (instructions == null)
                throw new ArgumentNullException("instructions");
            if (instructions.Count == 0)
                throw new ArgumentException("É necessária pelo menos uma instrução PG33.", "instructions");

            List<Tp02Pg33DryRunBlock> blocks = new List<Tp02Pg33DryRunBlock>();
            int cursor = startStep;
            int index = 0;

            while (index < instructions.Count)
            {
                if (cursor >= MaxProgramSteps)
                    throw new ArgumentOutOfRangeException("instructions", "Há instruções restantes depois do limite de 4000 passos.");

                int blockStart = cursor;
                int instructionCount = Math.Min(MaxInstructionsPerFrame, instructions.Count - index);
                int machineWordCount = 0;

                for (int local = 0; local < instructionCount; local++)
                {
                    Tp02Pg33DryRunInstruction instruction = instructions[index + local];
                    if (instruction == null)
                        throw new ArgumentException("Lista contém instrução PG33 nula.", "instructions");
                    if (instruction.StepSpan < 1 || instruction.StepSpan > 4)
                        throw new ArgumentOutOfRangeException("instructions", "Instrução contém StepSpan fora de 1..4.");
                    machineWordCount += instruction.Words.Length;
                }

                if (machineWordCount < 1 || machineWordCount > MaxMachineWordsPerFrame)
                    throw new ArgumentOutOfRangeException("instructions", "Bloco excede 80 palavras de máquina.");
                if (cursor + machineWordCount > MaxProgramSteps)
                    throw new ArgumentOutOfRangeException("instructions", "Bloco ultrapassa o limite de 4000 passos do TP02-40/60.");

                byte[] highLow = new byte[machineWordCount * 2];
                byte[] external = new byte[machineWordCount];
                int wordIndex = 0;

                for (int local = 0; local < instructionCount; local++)
                {
                    Tp02Pg33DryRunInstruction instruction = instructions[index + local];
                    for (int w = 0; w < instruction.Words.Length; w++)
                    {
                        Tp02MachineWord word = instruction.Words[w];
                        highLow[2 * wordIndex] = word.High;
                        highLow[(2 * wordIndex) + 1] = word.Low;
                        external[wordIndex] = word.External;
                        wordIndex++;
                    }
                    cursor += instruction.StepSpan;
                }

                byte[] frame = Tp02Pg33DryRunFrame.BuildCandidate(blockStart, highLow, external);
                blocks.Add(new Tp02Pg33DryRunBlock(
                    blockStart,
                    cursor,
                    instructionCount,
                    machineWordCount,
                    frame));
                index += instructionCount;
            }

            return blocks;
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Compila um arquivo .pladder do OpenLadder para palavras TP02 de 3 bytes.
    /// Nao abre porta serial e nao transmite ao PLC.
    /// </summary>
    internal static class TP02ProjectToWbpHexProgram
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
                {
                    Console.WriteLine("Uso: OpenLadderTP02ProjectExport.exe projeto.pladder [saida.hex]");
                    return 2;
                }

                string projectPath = Path.GetFullPath(args[0].Trim('"'));
                if (!File.Exists(projectPath)) throw new FileNotFoundException("Projeto nao encontrado.", projectPath);

                string outputPath = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
                    ? Path.GetFullPath(args[1].Trim('"'))
                    : Path.ChangeExtension(projectPath, ".tp02.hex");

                string source = File.ReadAllText(projectPath, Encoding.UTF8);
                LadderProjectDocument document = LadderProjectCodec.Deserialize(source);
                Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(document);

                Console.WriteLine(result.BuildReport());
                if (!result.Success)
                {
                    Console.Error.WriteLine("Compilacao TP02 recusada: corrija os erros antes de gerar WBP.");
                    return 1;
                }
                if (result.Words.Count < 1)
                {
                    Console.Error.WriteLine("Projeto sem passos TP02 compilados.");
                    return 1;
                }

                StringBuilder hex = new StringBuilder(result.Words.Count * 8);
                int i;
                for (i = 0; i < result.Words.Count; i++)
                    hex.AppendLine(result.Words[i].ToHex());

                File.WriteAllText(outputPath, hex.ToString(), Encoding.ASCII);
                Console.WriteLine("HEX TP02 criado: " + outputPath);
                Console.WriteLine("Passos: " + result.Words.Count.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Nenhum byte foi transmitido ao PLC.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERRO: " + ex.Message);
                return 1;
            }
        }
    }
}

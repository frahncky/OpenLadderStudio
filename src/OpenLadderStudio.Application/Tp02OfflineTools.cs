using System.Text;
using ModernPC12;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Application;

public sealed record Tp02WbpPreview(bool Success, int StepCount, IReadOnlyList<string> Frames, string Report);

public sealed record Tp02IlImport(bool Success, int InstructionCount, IReadOnlyList<string> Errors, string Report, LadderProjectModel? Project);

/// <summary>
/// Ferramentas TP02 que não falam com o PLC: quadros WBP para inspeção, arquivo HEX das
/// palavras compiladas e reconstrução IL -> Ladder. Usam o mesmo compilador e o mesmo
/// conversor do editor legado (núcleo). Nenhuma delas conhece porta serial.
/// </summary>
public sealed class Tp02OfflineTools
{
    public const int MaxStation = 99;
    public const int MaxStart = 4000;
    public const int MaxResponseCode = 15;

    /// <summary>Mesmo relatório da pré-compilação dry-run do editor legado.</summary>
    public Tp02WbpPreview PreviewWbp(LadderProjectModel model, int station = 1, int start = 0, int responseCode = 5)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegative(station);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(station, MaxStation);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, MaxStart);
        ArgumentOutOfRangeException.ThrowIfNegative(responseCode);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(responseCode, MaxResponseCode);

        Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(LadderProjectService.ToDocument(model));
        var report = new StringBuilder(result.BuildReport());
        if (!result.Success || result.Words.Count == 0)
            return new Tp02WbpPreview(false, result.Words.Count, Array.Empty<string>(), report.ToString());

        report.AppendLine();
        report.AppendLine("QUADROS WBP GERADOS SOMENTE PARA INSPECAO");
        report.AppendLine(new string('-', 66));
        List<string> frames = Tp02LadderTargetCompiler.BuildWbpDryRunFrames(result, station, start, responseCode);
        for (int i = 0; i < frames.Count; i++)
            report.Append("WBP[").Append(i.ToString("00")).Append("] ").AppendLine(frames[i].Replace("\r", "<CR>"));
        return new Tp02WbpPreview(true, result.Words.Count, frames, report.ToString());
    }

    /// <summary>
    /// Uma palavra de máquina por linha, em hexadecimal, como a ferramenta de exportação legada
    /// (TP02ProjectToWbpHex). Recusa programa com erro ou vazio.
    /// </summary>
    public string ExportHex(LadderProjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(LadderProjectService.ToDocument(model));
        if (!result.Success)
            throw new InvalidOperationException("Compilação TP02 recusada: corrija os erros antes de gerar o HEX. " + string.Join(" ", result.Errors));
        if (result.Words.Count < 1)
            throw new InvalidOperationException("Projeto sem passos TP02 compilados.");

        var hex = new StringBuilder(result.Words.Count * 8);
        foreach (Tp02MachineWord word in result.Words) hex.AppendLine(word.ToHex());
        return hex.ToString();
    }

    /// <summary>
    /// Reconstrói o projeto a partir de IL verificada do TP02. Só um subconjunto comprovável
    /// (STR, STR NOT, AND, AND NOT, OUT, END) é aceito; qualquer bloqueio impede a conversão.
    /// </summary>
    public Tp02IlImport ImportIl(string ilText, string sourceName = "")
    {
        ArgumentNullException.ThrowIfNull(ilText);
        string[] lines = ilText.Replace("\r\n", "\n").Split('\n');
        List<TP02IlInstruction> instructions = TP02IlToLadderConverter.Parse(lines);

        var report = new StringBuilder();
        report.AppendLine("TP02 — IL VERIFICADA → LADDER");
        report.AppendLine(new string('=', 84));
        if (sourceName.Length > 0) report.AppendLine("Arquivo: " + sourceName);
        report.AppendLine("Instruções reconhecidas: " + instructions.Count);

        if (instructions.Count == 0)
        {
            report.AppendLine();
            report.AppendLine("Nenhuma instrução IL reconhecida. Formato esperado por linha: NNNN: OPERAÇÃO OPERANDO");
            return new Tp02IlImport(false, 0, new[] { "Nenhuma instrução IL reconhecida." }, report.ToString(), null);
        }

        TP02IlConversion conversion = TP02IlToLadderConverter.Convert(instructions);
        report.AppendLine("Linhas candidatas: " + conversion.Rungs.Count);
        report.AppendLine("Erros/bloqueios: " + conversion.Errors.Count);
        report.AppendLine();

        if (!conversion.Success)
        {
            report.AppendLine("CONVERSÃO BLOQUEADA");
            report.AppendLine(new string('-', 84));
            foreach (string error in conversion.Errors) report.AppendLine("• " + error);
            report.AppendLine();
            report.AppendLine("Nenhum projeto é gerado enquanto houver instruções UNKNOWN, RAW ou estruturas não suportadas.");
            return new Tp02IlImport(false, instructions.Count, conversion.Errors.ToArray(), report.ToString(), null);
        }

        LadderProjectModel project = LadderProjectService.FromDocument(TP02IlToLadderConverter.BuildDocument(conversion.Rungs));
        report.AppendLine("CONVERSÃO LIBERADA");
        report.AppendLine(new string('-', 84));
        report.AppendLine(conversion.Rungs.Count + " linha(s) reconstruída(s). Nenhum byte foi lido nem transmitido ao PLC.");
        return new Tp02IlImport(true, instructions.Count, Array.Empty<string>(), report.ToString(), project);
    }
}

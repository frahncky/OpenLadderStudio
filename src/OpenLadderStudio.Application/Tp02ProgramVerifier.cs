using OpenLadderStudio.Core;

namespace OpenLadderStudio.Application;

public sealed record LadderVerificationStep(int Rung, int Step, string MachineHex, string Instruction);

public sealed class LadderVerificationResult
{
    internal LadderVerificationResult(
        string target,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings,
        IReadOnlyList<LadderVerificationStep> steps,
        string machineHex,
        string report)
    {
        Target = target;
        Errors = errors;
        Warnings = warnings;
        Steps = steps;
        MachineHex = machineHex;
        Report = report;
    }

    public string Target { get; }
    public bool Success => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<LadderVerificationStep> Steps { get; }

    /// <summary>Palavras de máquina concatenadas em hexadecimal, na ordem dos passos.</summary>
    public string MachineHex { get; }

    /// <summary>Relatório textual idêntico ao do editor legado.</summary>
    public string Report { get; }
}

/// <summary>
/// Verificação do programa para o TP02 (pré-compilação DRY-RUN). Usa o mesmo
/// Tp02LadderTargetCompiler do editor legado e nunca transmite ao controlador:
/// não conhece porta serial nem driver.
/// </summary>
public sealed class Tp02ProgramVerifier
{
    public string TargetName => "WEG TP02 (pré-compilação, sem transmissão)";

    public LadderVerificationResult Verify(LadderProjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(LadderProjectService.ToDocument(model));
        var steps = result.Trace
            .Select(row => new LadderVerificationStep(row.Rung, row.Step, row.MachineHex, row.BooleanText))
            .ToArray();

        return new LadderVerificationResult(
            TargetName,
            result.Errors.ToArray(),
            result.Warnings.ToArray(),
            steps,
            Tp02TargetCompiler.WordsToHex(result.Words),
            result.BuildReport());
    }
}

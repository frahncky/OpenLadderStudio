using System;
using OpenLadderStudio.Core;

internal static class Tp02PgProtocolSelfTest
{
    private static int failures;
    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }
    private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace('-', ' '); }

    public static int Main()
    {
        Check(Hex(Tp02PgProtocol.ProgramMode) == "01 00 FE", "modo Program do PC12");
        Check(Hex(Tp02PgProtocol.Run) == "02 00 FD", "RUN do PC12");
        Check(Hex(Tp02PgProtocol.Candidate03) == "03 00 FC", "candidato 03");
        Check(Hex(Tp02PgProtocol.Candidate04) == "04 00 FB", "candidato 04");
        Check(Hex(Tp02PgProtocol.ClearAllMemory) == "0F 00 F0", "Clear All Memory");
        Check(Hex(Tp02PgProtocol.Candidate11) == "11 00 EE", "candidato 11");
        Check(Hex(Tp02PgProtocol.Presence) == "F0 00 0F", "consulta F0");
        byte[][] frames = { Tp02PgProtocol.ProgramMode, Tp02PgProtocol.Run,
            Tp02PgProtocol.Candidate03, Tp02PgProtocol.Candidate04,
            Tp02PgProtocol.ClearAllMemory, Tp02PgProtocol.Candidate11, Tp02PgProtocol.Presence };
        for (int i = 0; i < frames.Length; i++)
            Check(Tp02PgProtocol.HasValidChecksum(frames[i]), "checksum do quadro " + i);
        Check(!Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Run), "RUN qualificado não bloqueado");
        Check(!Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Presence), "F0 de presença não bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.ProgramMode), "Program/STOP bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.ClearAllMemory), "Clear All bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate03), "03 bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate04), "04 bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate11), "11 bloqueado");
        System.Collections.Generic.IList<Tp02PgProtocol.CommandInfo> catalog = Tp02PgProtocol.GetCommandCatalog();
        Check(catalog.Count == 17, "catálogo completo com handshake e 16 opcodes");
        Check(catalog[0].Code == "HELLO" && catalog[16].Code == "F0", "ordem estável do catálogo");
        Check(catalog[3].Function.IndexOf("gravar programa", StringComparison.OrdinalIgnoreCase) >= 0,
            "03 associado ao preflight de escrita sem promover TX");
        Check(catalog[6].Function.IndexOf("V, D, WC, FILE", StringComparison.Ordinal) >= 0,
            "0A cataloga as áreas de leitura encontradas");
        int allowed = 0;
        for (int i = 0; i < catalog.Count; i++) if (catalog[i].TransmitAllowed) allowed++;
        Check(allowed == 5, "somente cinco operações qualificadas para TX");
        byte[] copy = Tp02PgProtocol.Copy(Tp02PgProtocol.Run);
        copy[0] = 0xFF;
        Check(Tp02PgProtocol.Run[0] == 0x02, "cópia defensiva");
        if (failures != 0) return 1;
        Console.WriteLine("Tp02PgProtocolSelfTest: OK (offline; nenhum TX)");
        return 0;
    }
}

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

    private static void MonitorChecks()
    {
        byte[] data = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        Tp02PgProtocol.MonitorResult r;

        r = Tp02PgProtocol.DecodeMonitor(0, 0, 0, data, 0);
        Check(r.Consumed == 0 && !r.HasState && !r.HasValue, "monitor tipo 0 não consome resposta");
        r = Tp02PgProtocol.DecodeMonitor(3, 0, 0, data, 0);
        Check(r.Consumed == 0 && !r.HasState && !r.HasValue, "monitor tipo 3 preserva posição");

        r = Tp02PgProtocol.DecodeMonitor(1, 1, 0, new byte[] { 0x02 }, 0);
        Check(r.Consumed == 1 && r.HasState && r.IsOn, "monitor tipo 1 ativo-alto");
        r = Tp02PgProtocol.DecodeMonitor(1, 1, 0, new byte[] { 0x00 }, 0);
        Check(!r.IsOn, "monitor tipo 1 Off com bit limpo");
        r = Tp02PgProtocol.DecodeMonitor(2, 1, 0, new byte[] { 0x00 }, 0);
        Check(r.Consumed == 1 && r.HasState && r.IsOn, "monitor tipo 2 ativo-baixo");
        r = Tp02PgProtocol.DecodeMonitor(2, 1, 0, new byte[] { 0x02 }, 0);
        Check(!r.IsOn, "monitor tipo 2 Off com bit setado");

        r = Tp02PgProtocol.DecodeMonitor(4, 0, 1, data, 0);
        Check(r.Consumed == 4 && r.HasValue && r.Value == 0x12u, "monitor tipo 4 largura 1 usa b0");
        r = Tp02PgProtocol.DecodeMonitor(4, 0, 2, data, 0);
        Check(r.Value == 0x3412u, "monitor tipo 4 largura 2 little-endian");
        r = Tp02PgProtocol.DecodeMonitor(4, 0, 3, data, 0);
        Check(r.Value == 0x78563412u, "monitor tipo 4 largura 3 little-endian 32");

        r = Tp02PgProtocol.DecodeMonitor(7, 0, 1, data, 0);
        Check(r.Consumed == 4 && r.HasValue && r.Value == 0x34u, "monitor tipo 7 largura 1 usa b1");
        r = Tp02PgProtocol.DecodeMonitor(7, 0, 2, data, 0);
        Check(r.Value == 0x1234u, "monitor tipo 7 largura 2 big-endian");
        r = Tp02PgProtocol.DecodeMonitor(7, 0, 3, data, 0);
        Check(r.Value == 0x56781234u, "monitor tipo 7 largura 3 por palavras");

        r = Tp02PgProtocol.DecodeMonitor(5, 0x34, 0, data, 0);
        Check(r.Consumed == 2 && r.HasState && r.IsOn, "monitor tipo 5 igualdade em b1");
        r = Tp02PgProtocol.DecodeMonitor(5, 0x35, 0, data, 0);
        Check(!r.IsOn, "monitor tipo 5 desigual fica Off");
        r = Tp02PgProtocol.DecodeMonitor(6, 0x35, 0, data, 0);
        Check(r.Consumed == 2 && r.HasState && r.IsOn, "monitor tipo 6 desigualdade em b1");
        r = Tp02PgProtocol.DecodeMonitor(6, 0x34, 0, data, 0);
        Check(!r.IsOn, "monitor tipo 6 igualdade fica Off");

        try { Tp02PgProtocol.DecodeMonitor(4, 0, 2, new byte[] { 1, 2, 3 }, 0); Check(false, "monitor Q4 incompleto rejeitado"); }
        catch (ArgumentException) { Check(true, "monitor Q4 incompleto rejeitado"); }
        try { Tp02PgProtocol.DecodeMonitor(1, 8, 0, new byte[] { 0 }, 0); Check(false, "monitor seletor de bit inválido rejeitado"); }
        catch (ArgumentOutOfRangeException) { Check(true, "monitor seletor de bit inválido rejeitado"); }
        try { Tp02PgProtocol.DecodeMonitor(7, 0, 4, data, 0); Check(false, "monitor largura inválida rejeitada"); }
        catch (ArgumentOutOfRangeException) { Check(true, "monitor largura inválida rejeitada"); }
    }

    public static int Main()
    {
        Check(Hex(Tp02PgProtocol.ProgramMode) == "01 00 FE", "modo Program do PC12");
        Check(Hex(Tp02PgProtocol.Run) == "02 00 FD", "RUN do PC12");
        Check(Hex(Tp02PgProtocol.Candidate03) == "03 00 FC", "candidato 03");
        Check(Hex(Tp02PgProtocol.Candidate04) == "04 00 FB", "candidato 04");
        Check(Hex(Tp02PgProtocol.ClearAllMemory) == "0F 00 F0", "Clear All Memory");
        Check(Hex(Tp02PgProtocol.Candidate11) == "11 00 EE", "candidato 11");
        Check(Hex(Tp02PgProtocol.EepromToPlc) == "12 00 ED", "EEPROM para PLC");
        Check(Hex(Tp02PgProtocol.PlcToEeprom) == "13 00 EC", "PLC para EEPROM");
        Check(Hex(Tp02PgProtocol.AuthorizationGate) == "14 00 EB", "gate de autorização PG14");
        Check(Hex(Tp02PgProtocol.Presence) == "F0 00 0F", "consulta F0");
        byte[][] frames = { Tp02PgProtocol.ProgramMode, Tp02PgProtocol.Run,
            Tp02PgProtocol.Candidate03, Tp02PgProtocol.Candidate04,
            Tp02PgProtocol.ClearAllMemory, Tp02PgProtocol.Candidate11,
            Tp02PgProtocol.EepromToPlc, Tp02PgProtocol.PlcToEeprom,
            Tp02PgProtocol.AuthorizationGate, Tp02PgProtocol.Presence };
        for (int i = 0; i < frames.Length; i++)
            Check(Tp02PgProtocol.HasValidChecksum(frames[i]), "checksum do quadro " + i);
        Check(!Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Run), "RUN qualificado não bloqueado");
        Check(!Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Presence), "F0 de preflight não bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.ProgramMode), "Program/STOP bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.ClearAllMemory), "Clear All bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate03), "03 bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate04), "04 bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.Candidate11), "11 bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.EepromToPlc), "12 EEPROM bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.PlcToEeprom), "13 EEPROM bloqueado");
        Check(Tp02PgProtocol.IsBlocked(Tp02PgProtocol.AuthorizationGate), "14 autorização bloqueado");
        System.Collections.Generic.IList<Tp02PgProtocol.CommandInfo> catalog = Tp02PgProtocol.GetCommandCatalog();
        Check(catalog.Count == 18, "catálogo completo com handshake e 17 opcodes");
        Check(catalog[0].Code == "HELLO" && catalog[17].Code == "F0", "ordem estável do catálogo");
        Check(catalog[3].Function.IndexOf("Clear Program", StringComparison.Ordinal) >= 0,
            "03 mapeado ao Clear Program sem promover TX");
        Check(catalog[4].Function == "Clear System", "04 mapeado ao Clear System");
        Check(catalog[8].Function == "Clear Data", "11 mapeado ao Clear Data");
        Check(catalog[9].Code == "12" && catalog[9].Function == "EEPROM PACK → PLC", "12 e direção EEPROM para PLC");
        Check(catalog[10].Code == "13" && catalog[10].Function == "PLC → EEPROM PACK", "13 e direção PLC para EEPROM");
        Check(catalog[11].Code == "14" && catalog[11].Function.IndexOf("Autorizar operação protegida", StringComparison.Ordinal) >= 0,
            "14 classificado como gate de autorização");
        Check(catalog[17].Function.IndexOf("Preflight/qualificação", StringComparison.Ordinal) >= 0,
            "F0 classificado como preflight de sessão, não STOP");
        Check(catalog[6].Function.IndexOf("V, D, WC, FILE", StringComparison.Ordinal) >= 0,
            "0A cataloga as áreas de leitura encontradas");
        Check(catalog[6].Evidence.IndexOf("parser nativo", StringComparison.Ordinal) >= 0,
            "0A registra parser do monitor tipos 0..7");
        int allowed = 0;
        for (int i = 0; i < catalog.Count; i++) if (catalog[i].TransmitAllowed) allowed++;
        Check(allowed == 5, "somente cinco operações qualificadas para TX");
        byte[] copy = Tp02PgProtocol.Copy(Tp02PgProtocol.Run);
        copy[0] = 0xFF;
        Check(Tp02PgProtocol.Run[0] == 0x02, "cópia defensiva");
        MonitorChecks();
        if (failures != 0) return 1;
        Console.WriteLine("Tp02PgProtocolSelfTest: OK (offline; nenhum TX)");
        return 0;
    }
}

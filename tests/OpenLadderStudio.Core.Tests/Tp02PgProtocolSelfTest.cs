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
    private static void Reject(Action action, string message)
    {
        try { action(); Check(false, message); }
        catch (ArgumentException) { Check(true, message); }
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
        Check(catalog[6].Evidence.IndexOf("70006=CNT", StringComparison.Ordinal) >= 0,
            "0A registra a identidade CNT comprovada do token 70006");
        int allowed = 0;
        for (int i = 0; i < catalog.Count; i++) if (catalog[i].TransmitAllowed) allowed++;
        Check(allowed == 5, "somente cinco operações qualificadas para TX");

        byte[] q4 = { 0x00, 0x04, 0x01, 0x02, 0x03, 0x04, 0xF1 };
        uint type4 = Tp02PgProtocol.DecodeMonitorQ4(q4, Tp02PgProtocol.MonitorQ4Type.Type4);
        uint type7 = Tp02PgProtocol.DecodeMonitorQ4(q4, Tp02PgProtocol.MonitorQ4Type.Type7);
        Check(type4 == 0x04030201u, "Q4 tipo 4 preserva ordem nativa");
        Check(type7 == 0x03040102u, "Q4 tipo 7 troca bytes por palavra como PC12");
        Check(Tp02PgProtocol.MonitorQ4TypeFromSelector(true) == Tp02PgProtocol.MonitorQ4Type.Type4,
            "seletor não-zero escolhe tipo 4");
        Check(Tp02PgProtocol.MonitorQ4TypeFromSelector(false) == Tp02PgProtocol.MonitorQ4Type.Type7,
            "seletor zero escolhe tipo 7");
        Check(Tp02PgProtocol.FormatMonitorQ4Decimal(type4) == "0067305985", "Q4 decimal %010u");
        Check(Tp02PgProtocol.FormatMonitorQ4Hex(type4) == "04030201", "Q4 hexadecimal %08X");
        Reject(delegate { Tp02PgProtocol.DecodeMonitorQ4(new byte[] { 0, 2, 1, 2, 0xFA }, Tp02PgProtocol.MonitorQ4Type.Type4); },
            "Q4 rejeita comprimento diferente de quatro bytes");
        Reject(delegate { Tp02PgProtocol.DecodeMonitorQ4(new byte[] { 0, 4, 1, 2, 3, 4, 0xF0 }, Tp02PgProtocol.MonitorQ4Type.Type4); },
            "Q4 rejeita checksum inválido");
        Reject(delegate { Tp02PgProtocol.DecodeMonitorQ4(q4, (Tp02PgProtocol.MonitorQ4Type)99); },
            "Q4 rejeita tipo interno desconhecido");

        byte[] copy = Tp02PgProtocol.Copy(Tp02PgProtocol.Run);
        copy[0] = 0xFF;
        Check(Tp02PgProtocol.Run[0] == 0x02, "cópia defensiva");
        if (failures != 0) return 1;
        Console.WriteLine("Tp02PgProtocolSelfTest: OK (offline; nenhum TX)");
        return 0;
    }
}

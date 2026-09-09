using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ModernPC12
{
    internal enum PlcMemoryAreaKind
    {
        Coil,
        DiscreteInput,
        HoldingRegister,
        InputRegister,
        VendorSpecific
    }

    internal sealed class PlcMemoryArea
    {
        public string Name;
        public PlcMemoryAreaKind Kind;
        public int StartAddress;
        public int Length;
        public string Prefix;
        public string Notes;

        public PlcMemoryArea()
        {
            Name = "Nova área";
            Kind = PlcMemoryAreaKind.HoldingRegister;
            StartAddress = 0;
            Length = 10;
            Prefix = "HR";
            Notes = string.Empty;
        }
    }

    internal static class PlcMemoryMapStore
    {
        private static string Root
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "OpenLadder Studio", "memorymaps");
            }
        }

        public static List<PlcMemoryArea> Load(PlcDeviceProfile profile)
        {
            string id = profile == null ? "default" : profile.Id;
            string file = FileFor(id);
            if (!File.Exists(file)) return CreateDefaults(profile);

            try
            {
                List<PlcMemoryArea> result = new List<PlcMemoryArea>();
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] p = line.Split(new char[] { '\t' });
                    if (p.Length < 6) continue;
                    PlcMemoryAreaKind kind;
                    int start;
                    int length;
                    if (!Enum.TryParse<PlcMemoryAreaKind>(p[1], true, out kind)) continue;
                    if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) continue;
                    if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out length)) continue;

                    PlcMemoryArea a = new PlcMemoryArea();
                    a.Name = Unescape(p[0]);
                    a.Kind = kind;
                    a.StartAddress = Math.Max(0, Math.Min(65535, start));
                    int available = Math.Max(1, 65536 - a.StartAddress);
                    a.Length = Math.Max(1, Math.Min(available, length));
                    a.Prefix = Unescape(p[4]);
                    a.Notes = Unescape(p[5]);
                    result.Add(a);
                }
                return result.Count == 0 ? CreateDefaults(profile) : result;
            }
            catch
            {
                return CreateDefaults(profile);
            }
        }

        public static void Save(PlcDeviceProfile profile, IList<PlcMemoryArea> areas)
        {
            string id = profile == null ? "default" : profile.Id;
            Directory.CreateDirectory(Root);
            List<string> lines = new List<string>();
            lines.Add("# OpenLadder Studio - mapa de memória por controlador");
            if (areas != null)
            {
                for (int i = 0; i < areas.Count; i++)
                {
                    PlcMemoryArea a = areas[i];
                    if (a == null) continue;
                    if (a.StartAddress < 0 || a.StartAddress > 65535) throw new InvalidOperationException("Endereço inicial fora da faixa Modbus.");
                    if (a.Length < 1 || (long)a.StartAddress + (long)a.Length > 65536L) throw new InvalidOperationException("A área '" + a.Name + "' ultrapassa o endereço Modbus 65535.");
                    lines.Add(Escape(a.Name) + "\t" + a.Kind.ToString() + "\t" + a.StartAddress.ToString(CultureInfo.InvariantCulture) + "\t" + a.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Escape(a.Prefix) + "\t" + Escape(a.Notes));
                }
            }
            File.WriteAllLines(FileFor(id), lines.ToArray());
        }

        public static List<PlcMemoryArea> CreateDefaults(PlcDeviceProfile profile)
        {
            List<PlcMemoryArea> list = new List<PlcMemoryArea>();
            if (profile != null && (string.Equals(profile.DriverId, "generic.modbus.rtu", StringComparison.OrdinalIgnoreCase) || string.Equals(profile.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(Area("Coils", PlcMemoryAreaKind.Coil, 0, 16, "C", "Área Modbus configurável."));
                list.Add(Area("Discrete Inputs", PlcMemoryAreaKind.DiscreteInput, 0, 16, "DI", "Área Modbus configurável."));
                list.Add(Area("Holding Registers", PlcMemoryAreaKind.HoldingRegister, 0, 10, "HR", "Área Modbus configurável."));
                list.Add(Area("Input Registers", PlcMemoryAreaKind.InputRegister, 0, 10, "IR", "Área Modbus configurável."));
            }
            return list;
        }

        private static PlcMemoryArea Area(string name, PlcMemoryAreaKind kind, int start, int length, string prefix, string notes)
        {
            PlcMemoryArea a = new PlcMemoryArea();
            a.Name = name;
            a.Kind = kind;
            a.StartAddress = start;
            a.Length = length;
            a.Prefix = prefix;
            a.Notes = notes;
            return a;
        }

        private static string FileFor(string id)
        {
            string safe = string.IsNullOrEmpty(id) ? "default" : id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return Path.Combine(Root, safe + ".map");
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static string Unescape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\\\", "\\");
        }
    }
}

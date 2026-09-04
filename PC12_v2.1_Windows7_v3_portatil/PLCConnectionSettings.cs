using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ModernPC12
{
    internal sealed class PlcConnectionSettings
    {
        public string ProfileId = string.Empty;
        public string Transport = "RTU";
        public string PortName = "COM1";
        public int BaudRate = 9600;
        public int DataBits = 8;
        public string Parity = "None";
        public int StopBits = 1;
        public string Host = "192.168.0.10";
        public int TcpPort = 502;
        public int UnitId = 1;
        public int TimeoutMs = 1000;
        public int DefaultFunction = 3;
        public int StartAddress = 0;
        public int Quantity = 10;
    }

    internal static class PlcConnectionSettingsStore
    {
        private static string Root
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "OpenLadder Studio", "connections");
            }
        }

        public static PlcConnectionSettings CreateDefaults(PlcDeviceProfile profile)
        {
            PlcConnectionSettings s = new PlcConnectionSettings();
            if (profile == null) return s;

            s.ProfileId = profile.Id;
            if (string.Equals(profile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                s.BaudRate = 19200;
                s.DataBits = 7;
                s.Parity = "Even";
                s.StopBits = 2;
            }
            else if (profile.Transport == PlcTransportKind.Tcp)
            {
                s.Transport = "TCP";
            }
            return s;
        }

        public static PlcConnectionSettings Load(PlcDeviceProfile profile)
        {
            PlcConnectionSettings s = CreateDefaults(profile);
            string id = profile == null ? "default" : profile.Id;
            string file = FileFor(id);
            if (!File.Exists(file)) return s;

            try
            {
                Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int p = line.IndexOf('=');
                    if (p <= 0) continue;
                    data[line.Substring(0, p).Trim()] = line.Substring(p + 1).Trim();
                }

                s.Transport = Get(data, "transport", s.Transport);
                s.PortName = Get(data, "port", s.PortName);
                s.BaudRate = GetInt(data, "baud", s.BaudRate, 300, 10000000);
                s.DataBits = GetInt(data, "dataBits", s.DataBits, 5, 8);
                s.Parity = Get(data, "parity", s.Parity);
                s.StopBits = GetInt(data, "stopBits", s.StopBits, 1, 2);
                s.Host = Get(data, "host", s.Host);
                s.TcpPort = GetInt(data, "tcpPort", s.TcpPort, 1, 65535);
                s.UnitId = GetInt(data, "unitId", s.UnitId, 1, 247);
                s.TimeoutMs = GetInt(data, "timeoutMs", s.TimeoutMs, 100, 60000);
                s.DefaultFunction = GetInt(data, "function", s.DefaultFunction, 1, 4);
                s.StartAddress = GetInt(data, "startAddress", s.StartAddress, 0, 65535);
                s.Quantity = GetInt(data, "quantity", s.Quantity, 1, 2000);
            }
            catch
            {
                return CreateDefaults(profile);
            }
            return s;
        }

        public static void Save(PlcDeviceProfile profile, PlcConnectionSettings s)
        {
            if (s == null) return;
            string id = profile == null ? s.ProfileId : profile.Id;
            if (string.IsNullOrEmpty(id)) id = "default";
            s.ProfileId = id;
            Directory.CreateDirectory(Root);
            File.WriteAllLines(FileFor(id), new string[]
            {
                "# OpenLadder Studio - conexão por controlador",
                "transport=" + s.Transport,
                "port=" + s.PortName,
                "baud=" + s.BaudRate.ToString(CultureInfo.InvariantCulture),
                "dataBits=" + s.DataBits.ToString(CultureInfo.InvariantCulture),
                "parity=" + s.Parity,
                "stopBits=" + s.StopBits.ToString(CultureInfo.InvariantCulture),
                "host=" + s.Host,
                "tcpPort=" + s.TcpPort.ToString(CultureInfo.InvariantCulture),
                "unitId=" + s.UnitId.ToString(CultureInfo.InvariantCulture),
                "timeoutMs=" + s.TimeoutMs.ToString(CultureInfo.InvariantCulture),
                "function=" + s.DefaultFunction.ToString(CultureInfo.InvariantCulture),
                "startAddress=" + s.StartAddress.ToString(CultureInfo.InvariantCulture),
                "quantity=" + s.Quantity.ToString(CultureInfo.InvariantCulture)
            });
        }

        private static string FileFor(string id)
        {
            string safe = string.IsNullOrEmpty(id) ? "default" : id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return Path.Combine(Root, safe + ".connection");
        }

        private static string Get(Dictionary<string, string> data, string key, string fallback)
        {
            string value;
            return data.TryGetValue(key, out value) && value.Length > 0 ? value : fallback;
        }

        private static int GetInt(Dictionary<string, string> data, string key, int fallback, int min, int max)
        {
            string text;
            int value;
            if (!data.TryGetValue(key, out text)) return fallback;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return fallback;
            if (value < min || value > max) return fallback;
            return value;
        }
    }
}

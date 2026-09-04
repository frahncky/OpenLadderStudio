using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ModernPC12
{
    internal static class CustomPlcProfileStore
    {
        private static string Root
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "OpenLadder Studio", "profiles");
            }
        }

        private static string ProfilesFile
        {
            get { return Path.Combine(Root, "custom.profiles"); }
        }

        public static List<PlcDeviceProfile> LoadAll()
        {
            List<PlcDeviceProfile> result = new List<PlcDeviceProfile>();
            if (!File.Exists(ProfilesFile)) return result;

            try
            {
                string[] lines = File.ReadAllLines(ProfilesFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] p = line.Split(new char[] { '\t' });
                    if (p.Length < 9) continue;

                    PlcDeviceProfile profile = new PlcDeviceProfile();
                    profile.Id = Unescape(p[0]);
                    profile.Manufacturer = Unescape(p[1]);
                    profile.Family = Unescape(p[2]);
                    profile.Model = Unescape(p[3]);
                    profile.Protocol = Unescape(p[4]);
                    profile.Transport = ParseTransport(p[5]);
                    profile.DriverId = Unescape(p[6]);
                    profile.SupportLevel = ParseSupport(p[7]);
                    profile.Notes = Unescape(p[8]);

                    if (profile.Id.Length == 0 || profile.Manufacturer.Length == 0 || profile.Model.Length == 0) continue;
                    result.Add(profile);
                }
            }
            catch
            {
                return new List<PlcDeviceProfile>();
            }

            return result;
        }

        public static PlcDeviceProfile Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            List<PlcDeviceProfile> profiles = LoadAll();
            for (int i = 0; i < profiles.Count; i++)
                if (string.Equals(profiles[i].Id, id, StringComparison.OrdinalIgnoreCase)) return profiles[i];
            return null;
        }

        public static bool IsCustom(PlcDeviceProfile profile)
        {
            return profile != null && !string.IsNullOrEmpty(profile.Id) && profile.Id.StartsWith("custom.", StringComparison.OrdinalIgnoreCase);
        }

        public static void Upsert(PlcDeviceProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            Validate(profile);

            List<PlcDeviceProfile> profiles = LoadAll();
            int existing = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i].Id, profile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    existing = i;
                    break;
                }
            }

            if (existing >= 0) profiles[existing] = profile;
            else profiles.Add(profile);
            SaveAll(profiles);
        }

        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            List<PlcDeviceProfile> profiles = LoadAll();
            for (int i = profiles.Count - 1; i >= 0; i--)
                if (string.Equals(profiles[i].Id, id, StringComparison.OrdinalIgnoreCase)) profiles.RemoveAt(i);
            SaveAll(profiles);
        }

        public static string CreateId(string manufacturer, string family, string model, string driverId)
        {
            string seed = Slug(manufacturer) + "." + Slug(family) + "." + Slug(model);
            if (seed.Replace(".", string.Empty).Length == 0) seed = "plc";
            string baseId = "custom." + seed;
            string id = baseId;
            int suffix = 2;
            while (Find(id) != null || PlcDriverRegistry.FindProfile(id) != null)
            {
                id = baseId + "." + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            return id;
        }

        private static void SaveAll(IList<PlcDeviceProfile> profiles)
        {
            Directory.CreateDirectory(Root);
            List<string> lines = new List<string>();
            lines.Add("# OpenLadder Studio - perfis personalizados de PLC");
            lines.Add("# id\tfabricante\tfamilia\tmodelo\tprotocolo\ttransporte\tdriver\tsuporte\tobservacao");

            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    PlcDeviceProfile p = profiles[i];
                    if (p == null) continue;
                    lines.Add(
                        Escape(p.Id) + "\t" +
                        Escape(p.Manufacturer) + "\t" +
                        Escape(p.Family) + "\t" +
                        Escape(p.Model) + "\t" +
                        Escape(p.Protocol) + "\t" +
                        p.Transport.ToString() + "\t" +
                        Escape(p.DriverId) + "\t" +
                        p.SupportLevel.ToString() + "\t" +
                        Escape(p.Notes));
                }
            }

            File.WriteAllLines(ProfilesFile, lines.ToArray(), Encoding.UTF8);
        }

        private static void Validate(PlcDeviceProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Id)) throw new InvalidOperationException("O perfil precisa de um identificador.");
            if (string.IsNullOrEmpty(profile.Manufacturer)) throw new InvalidOperationException("Informe o fabricante.");
            if (string.IsNullOrEmpty(profile.Model)) throw new InvalidOperationException("Informe o modelo.");
            if (string.IsNullOrEmpty(profile.DriverId)) throw new InvalidOperationException("Selecione um driver.");
            if (PlcDriverRegistry.FindDriver(profile.DriverId) == null) throw new InvalidOperationException("O driver selecionado não está registrado no OpenLadder Studio.");
        }

        private static PlcTransportKind ParseTransport(string value)
        {
            try { return (PlcTransportKind)Enum.Parse(typeof(PlcTransportKind), value, true); }
            catch { return PlcTransportKind.VendorSpecific; }
        }

        private static PlcSupportLevel ParseSupport(string value)
        {
            try { return (PlcSupportLevel)Enum.Parse(typeof(PlcSupportLevel), value, true); }
            catch { return PlcSupportLevel.Experimental; }
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

        private static string Slug(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string lower = value.Trim().ToLowerInvariant();
            StringBuilder sb = new StringBuilder();
            bool separator = false;
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    separator = false;
                }
                else if (!separator && sb.Length > 0)
                {
                    sb.Append('-');
                    separator = true;
                }
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == '-') sb.Length--;
            return sb.ToString();
        }
    }
}

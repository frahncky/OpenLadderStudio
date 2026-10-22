using System;
using System.Collections.Generic;

namespace OpenLadderStudio.Core
{
    /// <summary>Codificação de memória PG conferida no PC12 v2.1. Sem transporte.</summary>
    internal static class Tp02PgMemoryProtocol
    {
        internal enum Area { X, Y, C, SC, V, D, WC, FL, WS }

        internal static int Limit(Area area)
        {
            switch (area)
            {
                case Area.X: case Area.Y: return 384;
                case Area.C: case Area.D: return 2048;
                case Area.V: return 1024;
                case Area.WC: return 912;
                case Area.FL: return 130;
                case Area.SC: case Area.WS: return 128;
                default: throw new ArgumentOutOfRangeException("area");
            }
        }

        private static bool IsBit(Area area)
        {
            return area == Area.X || area == Area.Y || area == Area.C || area == Area.SC;
        }

        private static int Bank(Area area)
        {
            switch (area)
            {
                case Area.X: return 0x2000;
                case Area.Y: return 0x0000;
                case Area.C: return 0x1000;
                case Area.SC: return 0xA000;
                case Area.V: return 0x5000;
                case Area.D: return 0x9000;
                case Area.WC: return 0x7000;
                case Area.FL: return 0x8000;
                case Area.WS: return 0x6000;
                default: throw new ArgumentOutOfRangeException("area");
            }
        }

        private static int BytesPerAddress(Area area)
        {
            return IsBit(area) ? 1 : (area == Area.FL ? 20 : 2);
        }

        private static int AddressLimit(Area area)
        {
            return IsBit(area) ? Limit(area) / 8 : Limit(area);
        }

        private static int Address(Area area, int number)
        {
            if (number < 1 || number > Limit(area))
                throw new ArgumentOutOfRangeException("number", "Número fora da faixa da área.");
            return Bank(area) + (IsBit(area) ? (number - 1) / 8 : number - 1);
        }

        private static byte[] Complete(byte command, IList<byte> body)
        {
            if (body.Count > 255) throw new ArgumentException("Quadro excede 255 bytes de dados.");
            byte[] frame = new byte[body.Count + 3];
            frame[0] = command;
            frame[1] = (byte)body.Count;
            for (int i = 0; i < body.Count; i++) frame[i + 2] = body[i];
            frame[frame.Length - 1] = Tp02PgProtocol.Checksum(frame, frame.Length - 1);
            return frame;
        }

        private static byte[] RawRead(int address, int bytes)
        {
            if (address < 0 || address > 65535 || bytes < 1 || bytes > 255)
                throw new ArgumentOutOfRangeException("address", "Endereço ou quantidade inválidos.");
            return Complete(0x0A, new byte[] { (byte)(address >> 8), (byte)address, (byte)bytes });
        }

        internal static byte[] BuildRead(Area area, int number, int bytes)
        {
            int address = Address(area, number);
            int unit = BytesPerAddress(area);
            if (bytes < 1 || bytes > 255 || bytes % unit != 0)
                throw new ArgumentException("Quantidade incompatível com a unidade da área.");
            if (address - Bank(area) + bytes / unit > AddressLimit(area))
                throw new ArgumentException("A leitura ultrapassa o fim da área.");
            return RawRead(address, bytes);
        }

        internal static IList<byte[]> ReadAll(Area area)
        {
            if (area == Area.WS) return CreateSweep(RawRead(0x6000, 0xAC), 2);
            int bytes = IsBit(area) ? 16 : (area == Area.WC ? 114 : (area == Area.FL ? 200 : 128));
            return CreateSweep(RawRead(Bank(area), bytes), 256);
        }

        /// <summary>O endereço avança por registrador/arquivo; Q continua em bytes.</summary>
        internal static IList<byte[]> CreateSweep(byte[] initial, int maxReads)
        {
            ValidateRead(initial);
            if (maxReads < 1 || maxReads > 256) throw new ArgumentOutOfRangeException("maxReads");
            int address = (initial[2] << 8) | initial[3];
            int bytes = initial[4];
            List<byte[]> result = new List<byte[]>();
            // Duas consultas específicas do original. Não inferir outras páginas WS.
            if ((address == 0x6000 || address == 0x60AC) && bytes == 0xAC)
            {
                result.Add(RawRead(address, bytes));
                if (address == 0x6000 && maxReads > 1) result.Add(RawRead(0x60AC, bytes));
                return result.AsReadOnly();
            }
            Area area = FindArea(address);
            if (area == Area.WS)
                throw new ArgumentException("Passo de varredura WS não estabelecido para este pedido.");
            int unit = BytesPerAddress(area);
            if (bytes % unit != 0)
                throw new ArgumentException("Quantidade deve conter registradores ou arquivos completos.");
            int end = Bank(area) + AddressLimit(area);
            while (address < end && result.Count < maxReads)
            {
                int count = Math.Min(bytes / unit, end - address);
                result.Add(RawRead(address, count * unit));
                address += count; // nunca mascarar com FFFF: fim de área encerra a leitura
            }
            return result.AsReadOnly();
        }

        private static Area FindArea(int address)
        {
            foreach (Area area in Enum.GetValues(typeof(Area)))
                if (address >= Bank(area) && address < Bank(area) + AddressLimit(area)) return area;
            throw new ArgumentException("Endereço sem área PG identificada; varredura não montada.");
        }

        private static void ValidateRead(byte[] frame)
        {
            if (frame == null || frame.Length != 6 || frame[0] != 0x0A || frame[1] != 3
                || frame[4] == 0 || !Tp02PgProtocol.HasValidChecksum(frame))
                throw new ArgumentException("Pedido deve ser 0A 03 END QTD CHK com checksum válido.");
        }

        internal static byte[] BuildMultipleRead(IList<byte[]> requests)
        {
            if (requests == null || requests.Count == 0) throw new ArgumentException("Informe as leituras.");
            List<byte> body = new List<byte>();
            int expected = 0;
            foreach (byte[] request in requests)
            {
                ValidateRead(request);
                expected += request[4];
                if (expected > 255 || body.Count + 3 > 255)
                    throw new ArgumentException("Divida as leituras: pedido ou resposta excede 255 bytes.");
                body.Add(request[2]); body.Add(request[3]); body.Add(request[4]);
            }
            return Complete(0x0A, body);
        }

        internal static IList<byte[]> BuildRegisterWrites(Area area, IList<int> numbers, IList<ushort> values)
        {
            if (area != Area.V && area != Area.D && area != Area.WC && area != Area.WS)
                throw new ArgumentException("Escrita de registradores disponível para V, D, WC e WS.");
            if (numbers == null || values == null || numbers.Count == 0 || numbers.Count != values.Count)
                throw new ArgumentException("Informe um valor para cada registrador.");
            List<byte[]> result = new List<byte[]>();
            List<byte> body = new List<byte>();
            for (int i = 0; i < numbers.Count; i++)
            {
                if (area == Area.WS && !SystemWriteKnown(numbers[i]))
                    throw new ArgumentException("WS fora da seleção de escrita de sistema identificada.");
                int address = Address(area, numbers[i]);
                body.Add((byte)(address >> 8)); body.Add((byte)address); body.Add(2);
                body.Add((byte)(values[i] >> 8)); body.Add((byte)values[i]);
                if (body.Count == 200 || area == Area.WS || i == numbers.Count - 1)
                {
                    result.Add(Complete(9, body));
                    body.Clear();
                }
            }
            return result.AsReadOnly();
        }

        private static bool SystemWriteKnown(int n)
        {
            return n == 4 || n == 12 || n == 49 || (n >= 18 && n <= 25)
                || (n >= 41 && n <= 47) || (n >= 58 && n <= 64) || (n >= 67 && n <= 86);
        }

        internal static IList<byte[]> BuildSystemCoils(byte sc01To08, byte sc17To23)
        {
            if ((sc17To23 & 0x80) != 0) throw new ArgumentException("SC024 não integra o grupo de escrita identificado.");
            return new List<byte[]> {
                Complete(9, new byte[] { 0xA0, 0, 1, sc01To08 }),
                Complete(9, new byte[] { 0xA0, 2, 1, sc17To23 })
            }.AsReadOnly();
        }

        internal static byte[] BuildSetReset(Area area, int number, bool set)
        {
            if (area != Area.X && area != Area.Y && area != Area.C)
                throw new ArgumentException("SET/RESET PG35 identificado somente para X, Y e C.");
            Address(area, number);
            int index = (number - 1) / 8;
            int bank = area == Area.X ? 0x50 : (area == Area.Y ? 0x10 : 0x30);
            return Complete(0x35, new byte[] { (byte)(bank | (index >> 8)), (byte)index,
                (byte)(((number - 1) % 8) | (set ? 0x80 : 0)) });
        }

        internal static byte[] BuildFileWrite(int number, byte[] data)
        {
            if (data == null || data.Length != 20)
                throw new ArgumentException("Cada FL deve conter exatamente 20 bytes.");
            int address = Address(Area.FL, number);
            List<byte> body = new List<byte>(new byte[] { (byte)(address >> 8), (byte)address, 20 });
            body.AddRange(data); // não copiar a iteração excedente do ramo HEX do PC12
            return Complete(9, body);
        }

        internal static IList<byte[]> BuildFileWrites(IList<int> numbers, IList<byte[]> values)
        {
            if (numbers == null || values == null || numbers.Count == 0 || numbers.Count != values.Count)
                throw new ArgumentException("Informe 20 bytes para cada arquivo FL.");
            List<byte[]> result = new List<byte[]>();
            List<byte> body = new List<byte>();
            for (int i = 0; i < numbers.Count; i++)
            {
                byte[] one = BuildFileWrite(numbers[i], values[i]);
                for (int j = 2; j < one.Length - 1; j++) body.Add(one[j]);
                if (body.Count == 230 || i == numbers.Count - 1)
                {
                    result.Add(Complete(9, body));
                    body.Clear();
                }
            }
            return result.AsReadOnly();
        }

        internal static byte[] ReadClock() { return RawRead(0x53F9, 14); }
        internal static byte[] ReadScanTimes() { return RawRead(0x6000, 6); }

        // Ordem nativa: segundo, minuto, hora, dia, dia da semana, mês, ano sem século.
        internal static byte[] BuildClockWrite(int[] fields)
        {
            if (fields == null || fields.Length != 7) throw new ArgumentException("RTC exige sete campos.");
            int[] maxima = { 59, 59, 23, 31, 6, 12, 99 };
            for (int i = 0; i < fields.Length; i++)
                if (fields[i] < (i == 3 || i == 5 ? 1 : 0) || fields[i] > maxima[i])
                    throw new ArgumentException("Campo RTC fora da faixa: posição " + (i + 1));
            int[] days = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (fields[3] > days[fields[5] - 1]) throw new ArgumentException("Dia incompatível com o mês.");
            List<byte> body = new List<byte>(new byte[] { 0x53, 0xF9, 14 });
            foreach (int value in fields) { body.Add(0); body.Add((byte)value); }
            return Complete(9, body);
        }

        internal static byte[] Payload(byte[] response, int expected)
        {
            if (response == null || response.Length < 3 || response.Length != response[1] + 3
                || !Tp02PgProtocol.HasValidChecksum(response))
                throw new ArgumentException("Resposta incompleta ou checksum inválido.");
            if (response[0] != 0) throw new ArgumentException("Resposta de erro/status: " + response[0].ToString("X2"));
            if (expected >= 0 && response[1] != expected) throw new ArgumentException("Quantidade de dados inesperada.");
            byte[] data = new byte[response[1]];
            Array.Copy(response, 2, data, 0, data.Length);
            return data;
        }

        internal static ushort[] DecodeWords(byte[] response, int expectedWords)
        {
            if (expectedWords < 1 || expectedWords > 127) throw new ArgumentOutOfRangeException("expectedWords");
            byte[] data = Payload(response, expectedWords * 2);
            ushort[] result = new ushort[expectedWords];
            for (int i = 0; i < result.Length; i++) result[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);
            return result;
        }

        internal static ushort[] DecodeClock(byte[] response) { return DecodeWords(response, 7); }
        // O segundo campo é o mínimo; a ordem de exibição antiga invertia min/max.
        internal static ushort[] DecodeScanTimes(byte[] response) { return DecodeWords(response, 3); }
    }
}

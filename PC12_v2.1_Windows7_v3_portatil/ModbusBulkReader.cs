using System;
using System.Collections.Generic;
using System.Text;

namespace ModernPC12
{
    internal sealed class ModbusBulkFrame
    {
        public int Index;
        public int StartAddress;
        public int Quantity;
        public byte[] RawResponse;

        public ModbusBulkFrame()
        {
            RawResponse = new byte[0];
        }
    }

    internal sealed class ModbusBulkReadResult
    {
        public bool Success;
        public string Error;
        public bool[] Bits;
        public ushort[] Registers;
        public List<ModbusBulkFrame> Frames;
        public int RequestedQuantity;
        public int CompletedQuantity;

        public ModbusBulkReadResult()
        {
            Error = string.Empty;
            Bits = new bool[0];
            Registers = new ushort[0];
            Frames = new List<ModbusBulkFrame>();
        }
    }

    internal static class ModbusBulkReader
    {
        private delegate ModbusReadResult ReadBlockDelegate(ushort address, ushort quantity);

        public static int MaxPerRequest(ModbusFunction function)
        {
            return function == ModbusFunction.ReadCoils || function == ModbusFunction.ReadDiscreteInputs ? 2000 : 125;
        }

        public static int BlockCount(ModbusFunction function, int totalQuantity)
        {
            if (totalQuantity <= 0) return 0;
            int max = MaxPerRequest(function);
            return (totalQuantity + max - 1) / max;
        }

        public static ModbusBulkReadResult ReadRtu(ModbusRtuClient client, byte unit, ModbusFunction function, ushort startAddress, int totalQuantity, Action<int, int, int> progress)
        {
            if (client == null) throw new ArgumentNullException("client");
            return ReadInternal(delegate(ushort address, ushort quantity)
            {
                return client.Read(unit, function, address, quantity);
            }, function, startAddress, totalQuantity, progress);
        }

        public static ModbusBulkReadResult ReadTcp(ModbusTcpClient client, byte unit, ModbusFunction function, ushort startAddress, int totalQuantity, Action<int, int, int> progress)
        {
            if (client == null) throw new ArgumentNullException("client");
            return ReadInternal(delegate(ushort address, ushort quantity)
            {
                return client.Read(unit, function, address, quantity);
            }, function, startAddress, totalQuantity, progress);
        }

        private static ModbusBulkReadResult ReadInternal(ReadBlockDelegate reader, ModbusFunction function, ushort startAddress, int totalQuantity, Action<int, int, int> progress)
        {
            ModbusBulkReadResult output = new ModbusBulkReadResult();
            output.RequestedQuantity = totalQuantity;

            if (totalQuantity <= 0)
            {
                output.Error = "A quantidade total deve ser maior que zero.";
                return output;
            }

            if ((long)startAddress + (long)totalQuantity > 65536L)
            {
                output.Error = "A área solicitada ultrapassa o endereço Modbus 65535.";
                return output;
            }

            int maxPerRequest = MaxPerRequest(function);
            int totalBlocks = BlockCount(function, totalQuantity);
            List<bool> bits = new List<bool>();
            List<ushort> registers = new List<ushort>();
            int completed = 0;

            for (int block = 0; block < totalBlocks; block++)
            {
                int remaining = totalQuantity - completed;
                int chunk = Math.Min(maxPerRequest, remaining);
                int currentAddress = startAddress + completed;

                if (progress != null) progress(block + 1, totalBlocks, completed);

                ModbusReadResult result = reader((ushort)currentAddress, (ushort)chunk);
                ModbusBulkFrame frame = new ModbusBulkFrame();
                frame.Index = block + 1;
                frame.StartAddress = currentAddress;
                frame.Quantity = chunk;
                frame.RawResponse = result.RawResponse ?? new byte[0];
                output.Frames.Add(frame);

                if (!result.Success)
                {
                    output.Error = "Falha no bloco " + (block + 1).ToString() + "/" + totalBlocks.ToString() + " (endereço " + currentAddress.ToString() + ", quantidade " + chunk.ToString() + "): " + result.Error;
                    output.CompletedQuantity = completed;
                    output.Bits = bits.ToArray();
                    output.Registers = registers.ToArray();
                    return output;
                }

                if (result.Bits != null && result.Bits.Length > 0)
                    bits.AddRange(result.Bits);
                if (result.Registers != null && result.Registers.Length > 0)
                    registers.AddRange(result.Registers);

                completed += chunk;
                output.CompletedQuantity = completed;
                if (progress != null) progress(block + 1, totalBlocks, completed);
            }

            output.Bits = bits.ToArray();
            output.Registers = registers.ToArray();
            output.Success = completed == totalQuantity;
            if (!output.Success && string.IsNullOrEmpty(output.Error))
                output.Error = "A leitura foi concluída parcialmente.";
            return output;
        }

        public static string FormatRawFrames(IList<ModbusBulkFrame> frames)
        {
            if (frames == null || frames.Count == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < frames.Count; i++)
            {
                ModbusBulkFrame frame = frames[i];
                if (i > 0) sb.AppendLine();
                sb.Append("[Bloco ").Append(frame.Index).Append(" | endereço ").Append(frame.StartAddress).Append(" | qtd ").Append(frame.Quantity).AppendLine("]");
                byte[] data = frame.RawResponse ?? new byte[0];
                for (int j = 0; j < data.Length; j++)
                {
                    if (j > 0) sb.Append(' ');
                    sb.Append(data[j].ToString("X2"));
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }
}

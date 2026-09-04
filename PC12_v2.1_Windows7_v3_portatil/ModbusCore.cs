using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;

namespace ModernPC12
{
    internal enum ModbusFunction
    {
        ReadCoils = 1,
        ReadDiscreteInputs = 2,
        ReadHoldingRegisters = 3,
        ReadInputRegisters = 4
    }

    internal sealed class ModbusReadResult
    {
        public bool Success;
        public string Error;
        public bool[] Bits;
        public ushort[] Registers;
        public byte[] RawResponse;

        public ModbusReadResult()
        {
            Error = string.Empty;
            Bits = new bool[0];
            Registers = new ushort[0];
            RawResponse = new byte[0];
        }
    }

    internal static class ModbusProtocol
    {
        public static byte[] BuildPdu(ModbusFunction function, ushort startAddress, ushort quantity)
        {
            return new byte[]
            {
                (byte)function,
                (byte)(startAddress >> 8),
                (byte)(startAddress & 0xFF),
                (byte)(quantity >> 8),
                (byte)(quantity & 0xFF)
            };
        }

        public static ushort Crc16(byte[] data, int count)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < count; pos++)
            {
                crc ^= data[pos];
                for (int i = 0; i < 8; i++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb) crc ^= 0xA001;
                }
            }
            return crc;
        }

        public static ModbusReadResult ParsePdu(byte[] pdu, ModbusFunction expectedFunction, ushort quantity)
        {
            ModbusReadResult result = new ModbusReadResult();
            if (pdu == null || pdu.Length < 2)
            {
                result.Error = "Resposta Modbus incompleta.";
                return result;
            }

            byte function = pdu[0];
            if ((function & 0x80) != 0)
            {
                byte code = pdu.Length > 1 ? pdu[1] : (byte)0;
                result.Error = "Exceção Modbus " + code.ToString() + ": " + ExceptionText(code);
                return result;
            }

            if (function != (byte)expectedFunction)
            {
                result.Error = "Função inesperada na resposta Modbus.";
                return result;
            }

            int byteCount = pdu[1];
            if (pdu.Length < 2 + byteCount)
            {
                result.Error = "Quantidade de bytes inválida na resposta.";
                return result;
            }

            if (expectedFunction == ModbusFunction.ReadCoils || expectedFunction == ModbusFunction.ReadDiscreteInputs)
            {
                bool[] bits = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int dataIndex = 2 + (i / 8);
                    int mask = 1 << (i % 8);
                    bits[i] = (pdu[dataIndex] & mask) != 0;
                }
                result.Bits = bits;
            }
            else
            {
                if (byteCount < quantity * 2)
                {
                    result.Error = "Resposta com menos registradores do que o solicitado.";
                    return result;
                }

                ushort[] registers = new ushort[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    int index = 2 + (i * 2);
                    registers[i] = (ushort)((pdu[index] << 8) | pdu[index + 1]);
                }
                result.Registers = registers;
            }

            result.Success = true;
            return result;
        }

        public static string ExceptionText(byte code)
        {
            if (code == 1) return "função ilegal";
            if (code == 2) return "endereço de dados ilegal";
            if (code == 3) return "valor de dados ilegal";
            if (code == 4) return "falha no dispositivo escravo";
            if (code == 5) return "reconhecimento";
            if (code == 6) return "dispositivo ocupado";
            if (code == 8) return "erro de paridade de memória";
            if (code == 10) return "gateway indisponível";
            if (code == 11) return "gateway sem resposta";
            return "código não identificado";
        }
    }

    internal sealed class ModbusRtuClient
    {
        public string PortName;
        public int BaudRate;
        public int DataBits;
        public Parity Parity;
        public StopBits StopBits;
        public int TimeoutMs;

        public ModbusRtuClient()
        {
            PortName = "COM1";
            BaudRate = 9600;
            DataBits = 8;
            Parity = Parity.None;
            StopBits = StopBits.One;
            TimeoutMs = 1000;
        }

        public ModbusReadResult Read(byte unitId, ModbusFunction function, ushort startAddress, ushort quantity)
        {
            ModbusReadResult result = new ModbusReadResult();
            if (quantity == 0)
            {
                result.Error = "Quantidade deve ser maior que zero.";
                return result;
            }

            byte[] pdu = ModbusProtocol.BuildPdu(function, startAddress, quantity);
            byte[] request = new byte[1 + pdu.Length + 2];
            request[0] = unitId;
            Array.Copy(pdu, 0, request, 1, pdu.Length);
            ushort crc = ModbusProtocol.Crc16(request, request.Length - 2);
            request[request.Length - 2] = (byte)(crc & 0xFF);
            request[request.Length - 1] = (byte)(crc >> 8);

            SerialPort port = new SerialPort();
            try
            {
                port.PortName = PortName;
                port.BaudRate = BaudRate;
                port.DataBits = DataBits;
                port.Parity = Parity;
                port.StopBits = StopBits;
                port.ReadTimeout = TimeoutMs;
                port.WriteTimeout = TimeoutMs;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                port.Write(request, 0, request.Length);

                int expectedDataBytes = (function == ModbusFunction.ReadCoils || function == ModbusFunction.ReadDiscreteInputs)
                    ? ((quantity + 7) / 8)
                    : quantity * 2;
                int expectedLength = 5 + expectedDataBytes;
                byte[] response = ReadSerialFrame(port, expectedLength, TimeoutMs);
                result.RawResponse = response;

                if (response.Length < 5)
                {
                    result.Error = "Resposta RTU incompleta.";
                    return result;
                }

                ushort receivedCrc = (ushort)(response[response.Length - 2] | (response[response.Length - 1] << 8));
                ushort calculatedCrc = ModbusProtocol.Crc16(response, response.Length - 2);
                if (receivedCrc != calculatedCrc)
                {
                    result.Error = "CRC inválido na resposta Modbus RTU.";
                    return result;
                }

                if (response[0] != unitId)
                {
                    result.Error = "Unit ID diferente do solicitado.";
                    return result;
                }

                byte[] responsePdu = new byte[response.Length - 3];
                Array.Copy(response, 1, responsePdu, 0, responsePdu.Length);
                ModbusReadResult parsed = ModbusProtocol.ParsePdu(responsePdu, function, quantity);
                parsed.RawResponse = response;
                return parsed;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                try { if (port.IsOpen) port.Close(); }
                catch { }
                port.Dispose();
            }
        }

        private static byte[] ReadSerialFrame(SerialPort port, int expectedLength, int timeoutMs)
        {
            List<byte> bytes = new List<byte>();
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                while (port.BytesToRead > 0)
                {
                    bytes.Add((byte)port.ReadByte());
                    if (bytes.Count >= 5)
                    {
                        if ((bytes[1] & 0x80) != 0 && bytes.Count >= 5) return bytes.ToArray();
                        int count = bytes[2];
                        int dynamicLength = 5 + count;
                        if (bytes.Count >= dynamicLength) return bytes.ToArray();
                    }
                    if (bytes.Count >= expectedLength) return bytes.ToArray();
                }
                System.Threading.Thread.Sleep(5);
            }
            return bytes.ToArray();
        }
    }

    internal sealed class ModbusTcpClient
    {
        private static ushort nextTransactionId = 1;

        public string Host;
        public int Port;
        public int TimeoutMs;

        public ModbusTcpClient()
        {
            Host = "127.0.0.1";
            Port = 502;
            TimeoutMs = 1000;
        }

        public ModbusReadResult Read(byte unitId, ModbusFunction function, ushort startAddress, ushort quantity)
        {
            ModbusReadResult result = new ModbusReadResult();
            if (quantity == 0)
            {
                result.Error = "Quantidade deve ser maior que zero.";
                return result;
            }

            ushort transactionId = nextTransactionId++;
            byte[] pdu = ModbusProtocol.BuildPdu(function, startAddress, quantity);
            ushort length = (ushort)(1 + pdu.Length);
            byte[] request = new byte[7 + pdu.Length];
            request[0] = (byte)(transactionId >> 8);
            request[1] = (byte)(transactionId & 0xFF);
            request[2] = 0;
            request[3] = 0;
            request[4] = (byte)(length >> 8);
            request[5] = (byte)(length & 0xFF);
            request[6] = unitId;
            Array.Copy(pdu, 0, request, 7, pdu.Length);

            TcpClient client = new TcpClient();
            try
            {
                IAsyncResult ar = client.BeginConnect(Host, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeoutMs, false))
                {
                    result.Error = "Tempo esgotado ao conectar ao dispositivo Modbus TCP.";
                    return result;
                }
                client.EndConnect(ar);
                client.ReceiveTimeout = TimeoutMs;
                client.SendTimeout = TimeoutMs;

                NetworkStream stream = client.GetStream();
                stream.Write(request, 0, request.Length);

                byte[] header = ReadExact(stream, 7);
                ushort receivedTransaction = (ushort)((header[0] << 8) | header[1]);
                ushort protocol = (ushort)((header[2] << 8) | header[3]);
                ushort responseLength = (ushort)((header[4] << 8) | header[5]);

                if (receivedTransaction != transactionId)
                {
                    result.Error = "Transaction ID inválido na resposta Modbus TCP.";
                    return result;
                }
                if (protocol != 0)
                {
                    result.Error = "Protocol ID inválido na resposta Modbus TCP.";
                    return result;
                }
                if (responseLength < 2)
                {
                    result.Error = "Comprimento inválido na resposta Modbus TCP.";
                    return result;
                }

                int pduLength = responseLength - 1;
                byte[] responsePdu = ReadExact(stream, pduLength);
                byte[] raw = new byte[7 + responsePdu.Length];
                Array.Copy(header, raw, 7);
                Array.Copy(responsePdu, 0, raw, 7, responsePdu.Length);

                ModbusReadResult parsed = ModbusProtocol.ParsePdu(responsePdu, function, quantity);
                parsed.RawResponse = raw;
                return parsed;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                try { client.Close(); }
                catch { }
            }
        }

        private static byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new InvalidOperationException("Conexão encerrada antes do fim da resposta Modbus TCP.");
                offset += read;
            }
            return buffer;
        }
    }
}

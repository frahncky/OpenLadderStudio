using System;
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
}

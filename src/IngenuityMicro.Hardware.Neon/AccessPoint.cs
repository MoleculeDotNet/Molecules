using System;
using IngenuityMicro.Net;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Neon
{
    public class AccessPoint
    {
        internal AccessPoint(Ecn ecn, string ssid, int rssi, string macAddress, bool automaticMode)
        {
            this.Ecn = ecn;
            this.Ssid = ssid;
            this.Rssi = rssi;
            this.MacAddress = macAddress;
            this.AutomaticConnectionMode = automaticMode;
        }

        public Ecn Ecn { get; private set; }
        public string Ssid { get; private set; }
        public int Rssi { get; private set; }
        public string MacAddress { get; private set; }
        public bool AutomaticConnectionMode { get; private set; }
    }
}

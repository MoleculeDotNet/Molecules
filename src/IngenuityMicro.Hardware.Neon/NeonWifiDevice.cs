using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.IO.Ports;
using PervasiveDigital.Hardware.ESP8266;

namespace IngenuityMicro.Hardware.Neon
{
    public sealed class NeonWifiDevice : Esp8266WifiDevice
    {
        public NeonWifiDevice()
            : this("COM2")
        {
        }

        public NeonWifiDevice(string comPortName) :
            base(new SerialPort(comPortName, 115200, Parity.None, 8, StopBits.One), Oxygen.Hardware.RfPower, null)
        {
        }

        public NeonWifiDevice(string comPortName, OutputPort resetPin) :
            base(new SerialPort(comPortName, 115200, Parity.None, 8, StopBits.One), Oxygen.Hardware.RfPower, resetPin)
        {
        }

    }
}

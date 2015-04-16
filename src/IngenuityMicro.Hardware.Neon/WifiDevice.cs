using System;
using IngenuityMicro.Net;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Serial;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.IO.Ports;


namespace IngenuityMicro.Hardware.Neon
{
    public delegate void WifiBootedEventHandler(object sender, EventArgs args);
    public delegate void WifiErrorEventHandler(object sender, EventArgs args);
    public delegate void WifiConnectionStateEventHandler(object sender, EventArgs args);

    public class WifiDevice : IWifiAdapter, IDisposable
    {
        private readonly AtProtocolClient _neon;
        private readonly OutputPort _resetPin;
        private bool _fInitialized = false;

        public event WifiBootedEventHandler Booted;
        public event WifiErrorEventHandler Error;
        public event WifiConnectionStateEventHandler ConnectionStateChanged;

        public WifiDevice()
        {
            var port = new SerialPort("COM2", 115200, Parity.None, 8, StopBits.One);
            _neon = new AtProtocolClient(port);
            _neon.UnsolicitedNotificationReceived += OnUnsolicitedNotificationReceived;
            _neon.AddUnsolicitedNotifications(new string[]
            {

            });
            _neon.Start();
            _resetPin = new OutputPort(Pin.PB11, false);
        }

        private void OnUnsolicitedNotificationReceived(object sender, UnsolicitedNotificationEventArgs args)
        {
        }

        public void Connect(string ssid, string password)
        {
            EnsureInitialized();
        }

        public void SetPower(bool state)
        {
            Oxygen.Hardware.EnableRfPower(state);
        }

        public void Dispose()
        {
        }

        private void EnsureInitialized()
        {
            if (!Oxygen.Hardware.RfPower.Read())
                _fInitialized = false;
            if (_fInitialized)
                return;

            Oxygen.Hardware.EnableRfPower(true);

            _fInitialized = true;
        }
    }
}

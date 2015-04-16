using System;
using System.Collections;
using IngenuityMicro.Net;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Serial;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.IO.Ports;
using System.Net;
using System.Threading;


namespace IngenuityMicro.Hardware.Neon
{
    public delegate void WifiBootedEventHandler(object sender, EventArgs args);
    public delegate void WifiErrorEventHandler(object sender, EventArgs args);
    public delegate void WifiConnectionStateEventHandler(object sender, EventArgs args);

    public class WifiDevice : IWifiAdapter, IDisposable
    {
        public const string AT = "AT";
        public const string OK = "OK";
        public const string GetFirmwareVersionCommand = "AT+GMR";
        public const string GetAddressInformationCommand = "AT+CIFSR";
        public const string ListAccessPointsCommand = "AT+CWLAP";
        public const string JoinAccessPointCommand = "AT+CWJAP=";
        public const string QuitAccessPointCommand = "AT+CWQAP";

        private readonly AtProtocolClient _neon;
        private ManualResetEvent _isInitializedEvent = new ManualResetEvent(false);

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
                "AT version:", "SDK version:", "compile time:"
            });

            _neon.Start();
            new Thread(Initialize).Start();
        }

        public void Dispose()
        {
        }

        public void Connect(string ssid, string password)
        {
            EnsureInitialized();

            _neon.SendAndExpect(JoinAccessPointCommand + '"' + ssid + "\",\"" + password + '"', OK, -1);

            // Update our IP address
            GetAddressInformation();
        }

        public void Disconnect()
        {
            _neon.SendAndExpect(QuitAccessPointCommand, OK);
        }

        public void SetPower(bool state)
        {
            Oxygen.Hardware.EnableRfPower(state);
        }

        private IPAddress _address = IPAddress.Parse("0.0.0.0");

        public IPAddress IPAddress
        {
            get {  return _address; }
            private set { _address = value; }
        }

        public ManualResetEvent IsInitializedEvent { get {  return _isInitializedEvent; } }

        public string[] Version { get; private set; }

        public string MacAddress { get; private set; }

        public AccessPoint[] GetAccessPoints()
        {
            ArrayList result = new ArrayList();
            
            EnsureInitialized();

            var response = _neon.SendAndReadUntil(ListAccessPointsCommand, OK);
            foreach (var line in response)
            {
                var info = Utilities.Unquote(line.Substring(line.IndexOf(':') + 1));
                var tokens = info.Split(',');
                if (tokens.Length >= 4)
                {
                    var ecn = (Ecn) byte.Parse(tokens[0]);
                    var ssid = tokens[1];
                    var rssi = int.Parse(tokens[2]);
                    var mac = tokens[3];
                    bool mode = false;
                    if (tokens.Length >= 5)
                        mode = int.Parse(tokens[4]) != 0;
                    result.Add(new AccessPoint(ecn, ssid, rssi, mac, mode));
                }
            }
            return (AccessPoint[])result.ToArray(typeof(AccessPoint));
        }

        private void OnUnsolicitedNotificationReceived(object sender, UnsolicitedNotificationEventArgs args)
        {
        }

        private void Initialize()
        {
            bool success = false;
            int retries = 4;
            do
            {
                if (!Oxygen.Hardware.RfPower.Read())
                {
                    Oxygen.Hardware.EnableRfPower(true);
                    Thread.Sleep(500);
                }

                // Auto-baud
                _neon.SendCommand(AT);
                _neon.SendCommand(AT);
                _neon.SendCommand(AT);
                Thread.Sleep(100);
                try
                {
                    _neon.SendAndExpect(AT, OK);

                    this.Version = _neon.SendAndReadUntil(GetFirmwareVersionCommand, OK);

                    GetAddressInformation();

                    _isInitializedEvent.Set();

                    if (this.Booted != null)
                        this.Booted(this, new EventArgs());

                    success = true;
                }
                catch (FailedExpectException fee)
                {
                    // If we get a busy indication, then it is still booting - don't cycle the power
                    if (fee.Actual.IndexOf("busy")!=-1)
                        Thread.Sleep(500);
                    success = false;
                }
                catch (CommandTimeoutException)
                {
                    success = false;
                    Oxygen.Hardware.EnableRfPower(false);
                    Thread.Sleep(2000);
                }
            } while (!success && --retries > 0);
            if (!success)
            {
                throw new CommandTimeoutException("initialization failed");
            }
        }

        private void EnsureInitialized()
        {
            _isInitializedEvent.WaitOne();
        }

        private void GetAddressInformation()
        {
            var info = _neon.SendAndReadUntil("AT+CIFSR", OK);
            foreach (var line in info)
            {
                if (line.IndexOf("STAIP") != -1)
                {
                    var arg = Utilities.Unquote(line.Substring(line.IndexOf(',')+1));
                    this.IPAddress = IPAddress.Parse(arg);
                }
                else if (line.IndexOf("STAMAC") != -1)
                {
                    var arg = Utilities.Unquote(line.Substring(line.IndexOf(',')+1));
                    this.MacAddress = arg;
                }
            }
        }
    }
}

using System;
using System.Collections;
using IngenuityMicro.Net;
using IngenuityMicro.Hardware.Oxygen;
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
        public const string SetMuxModeCommand = "AT+CIPMUX=";
        public const string SessionStartCommand = "AT+CIPSTART=";
        public const string LinkedReply = "Linked";
        public const string SendCommand = "AT+CIPSEND=";
        public const string SendCommandReply = "SEND OK";
        public const string ConnectReply = "CONNECT";
        public const string ErrorReply = "ERROR";

        private readonly ManualResetEvent _isInitializedEvent = new ManualResetEvent(false);
        private readonly NeonSocket[] _sockets = new NeonSocket[4];
        private ESP8266Serial _neon;

        public event WifiBootedEventHandler Booted;
        //public event WifiErrorEventHandler Error;
        //public event WifiConnectionStateEventHandler ConnectionStateChanged;
        
        public WifiDevice() : this("COM2")
        {
        }

        public WifiDevice(string comPortName)
        {
            var port = new SerialPort(comPortName, 115200, Parity.None, 8, StopBits.One);
            Initialize(port);
        }

        private void Initialize(SerialPort port)
        {
            _neon = new ESP8266Serial(port);
            _neon.DataReceived += NeonOnDataReceived;
            _neon.SocketClosed += NeonOnSocketClosed;
            _neon.Start();
            new Thread(BackgroundInitialize).Start();
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

        public ISocket OpenSocket(string hostNameOrAddress, int portNumber, bool useTcp)
        {
            int iSocket = -1;
            for (int i = 0; i < _sockets.Length; ++i)
            {
                if (_sockets[i] == null)
                {
                    iSocket = i;
                    break;
                }
            }
            if (iSocket < 0)
            {
                throw new Exception("Too many sockets open - you must close one first.");
            }

            var result = new NeonSocket(this, iSocket, hostNameOrAddress, portNumber, useTcp);
            _sockets[iSocket] = result;

            return OpenSocket(iSocket);
        }

        internal NeonSocket OpenSocket(int socket)
        {
            // We should get back "n,CONNECT" where n is the socket number
            var sock = _sockets[socket];
            var reply = _neon.SendCommandAndReadReply(SessionStartCommand + socket + ',' + (sock.UseTcp ? "\"TCP\",\"" : "\"UDP\",\"") + sock.Hostname + "\"," + sock.Port);
            if (reply.IndexOf(ConnectReply) == -1)
                throw new FailedExpectException(SessionStartCommand, ConnectReply, reply);
            reply = reply.Substring(0, reply.IndexOf(','));
            if (int.Parse(reply) != socket)
                throw new Exception("Unexpected socket response");
            return sock;
        }

        internal void DeleteSocket(int socket)
        {
            if (socket >= 0 && socket <= _sockets.Length)
            {
                _sockets[socket] = null;
            }
        }

        internal void CloseSocket(int socket)
        {
            if (socket >= 0 && socket <= _sockets.Length)
            {
            }
        }

        internal void SendPayload(int iSocket, byte[] payload)
        {
            _neon.SendAndExpect(SendCommand + iSocket + ',' + payload.Length, OK);
            _neon.Write(payload);
            _neon.Find(SendCommandReply);
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
                var info = Unquote(line.Substring(line.IndexOf(':') + 1));
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

        private void NeonOnDataReceived(object sender, byte[] stream, int channel)
        {
            if (_sockets[channel] != null)
            {
                //REVIEW: would be more efficient to use the AtProtocolClient's event queue and not spin up new threads
                new Thread(() =>
                {
                    _sockets[channel].ReceivedData(stream);
                }).Start();
            }
        }

        private void NeonOnSocketClosed(object sender, int channel)
        {
            if (_sockets[channel] != null)
            {
                //REVIEW: would be more efficient to use the AtProtocolClient's event queue and not spin up new threads
                new Thread(() =>
                {
                    _sockets[channel].SocketClosedByPeer();
                }).Start();
            }
        }

        private void BackgroundInitialize()
        {
            bool success = false;
            int retries = 10;
            do
            {
                if (!Oxygen.Hardware.RfPower.Read())
                {
                    Thread.Sleep(2000);
                    SetPower(true);
                    Thread.Sleep(2000);
                }

                // Auto-baud
                _neon.SendCommand(AT);
                _neon.SendCommand(AT);
                _neon.SendCommand(AT);
                Thread.Sleep(100);
                try
                {
                    _neon.SendAndExpect(AT, OK, 2000);

                    SetMuxMode(true);

                    // Get the firmware version information
                    this.Version = _neon.SendAndReadUntil(GetFirmwareVersionCommand, OK);

                    // Collect the current IP address information
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
                    // known firmware problem. Clear the AP.
                    _neon.SendCommand(JoinAccessPointCommand + "\"\",\"\"");
                    if ((retries%1) == 0)
                    {
                        Oxygen.Hardware.EnableRfPower(false);
                    }
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
                    var arg = Unquote(line.Substring(line.IndexOf(',')+1));
                    this.IPAddress = IPAddress.Parse(arg);
                }
                else if (line.IndexOf("STAMAC") != -1)
                {
                    var arg = Unquote(line.Substring(line.IndexOf(',')+1));
                    this.MacAddress = arg;
                }
            }
        }

        private void SetMuxMode(bool enableMux)
        {
            _neon.SendAndExpect(SetMuxModeCommand + (enableMux ? '1' : '0'), OK);
        }

        private string Unquote(string quotedString)
        {
            quotedString = quotedString.Trim();
            var quoteChar = quotedString[0];
            if (quoteChar != '\'' && quoteChar != '"' && quoteChar != '(')
                return quotedString;
            if (quoteChar == '(')
                quoteChar = ')';
            if (quotedString.LastIndexOf(quoteChar) != quotedString.Length - 1)
                return quotedString;
            quotedString = quotedString.Substring(1);
            quotedString = quotedString.Substring(0, quotedString.Length - 1);
            return /* the now unquoted */ quotedString;
        }
    }
}

using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Touch;

namespace IngenuityMicro.Net
{
    public class SntpClient
    {
        private const int SNTP_PACKET_SIZE = 48;
        private byte[] _packet = new byte[SNTP_PACKET_SIZE];
        private readonly INetworkAdapter _adapter;
        private readonly string _host;
        private TimeSpan _pollingInterval;

        public SntpClient(INetworkAdapter adapter, string host)
            : this(adapter, host, new TimeSpan(0, 1, 0))
        {
        }

        public SntpClient(INetworkAdapter adapter, string host, TimeSpan pollingInterval)
        {
            if (pollingInterval.Ticks < new TimeSpan(0, 0, 15).Ticks)
            {
                throw new ArgumentOutOfRangeException("pollingInterval", "polling interval should be 15 seconds or more to avoid flooding the server");
            }
            _adapter = adapter;
            _host = host;
            _pollingInterval = pollingInterval;
        }

        public DateTime RequestTime()
        {
            SendRequest();
            return DateTime.MinValue;
        }

        public void SetTime()
        {
            SendRequest();
        }

        private void SendRequest()
        {
            using (var socket = _adapter.OpenSocket(_host, 123, false))
            {
                socket.DataReceived += OnDataReceived;
                Array.Clear(_packet,0,_packet.Length);
                _packet[0] = 0xe3; // LI, Version and Mode
                _packet[1] = 0; // Stratum or type of clock
                _packet[2] = 6; // Polling interval
                _packet[3] = 0xEC; // Peer clock precision
                // Leave eight bytes of zeros for root delay and root dispersion
                _packet[12] = 49;
                _packet[13] = 0x4E;
                _packet[14] = 49;
                _packet[15] = 52;
                socket.Send(_packet);
            }
        }

        private void OnDataReceived(object sender, SocketReceivedDataEventArgs args)
        {
            var data = args.Data;

            ulong timestamp = (ulong)(data[40] << 24 | data[41] << 16 | data[42] << 8 | data[43]);
            Debug.Print("Timestamp is : " + timestamp );
        }
    }
}

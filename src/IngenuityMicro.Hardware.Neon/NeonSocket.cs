using System;
using Microsoft.SPOT;
using System.Text;

namespace IngenuityMicro.Hardware.Neon
{
    public delegate void SocketReceivedDataEventHandler(object sender, SocketReceivedDataEventArgs args);
    public delegate void SocketClosedEventHandler(object sender, EventArgs args);

    public class NeonSocket
    {
        private readonly WifiDevice _parent;
        private readonly int _iSocket;

        public event SocketReceivedDataEventHandler DataReceived;
        public event SocketClosedEventHandler SocketClosed;

        internal NeonSocket(WifiDevice device, int iSocket)
        {
            _parent = device;
            _iSocket = iSocket;
        }

        public void Send(string payload)
        {
            Send(Encoding.UTF8.GetBytes(payload));
        }

        public void Send(byte[] payload)
        {
            _parent.SendPayload(_iSocket, payload);
        }

        public void Close()
        {
            if (SocketClosed != null)
            {
                SocketClosed(this, EventArgs.Empty);
            }
        }

        internal void ReceivedData(string data)
        {
            if (this.DataReceived != null)
                this.DataReceived(this, new SocketReceivedDataEventArgs(data));
        }

        internal void SocketClosedByPeer()
        {
            Close();
        }
    }

    public class SocketReceivedDataEventArgs : EventArgs
    {
        public SocketReceivedDataEventArgs(string data)
        {
            this.Data = data;
        }

        public string Data { get; private set; }
    }
}

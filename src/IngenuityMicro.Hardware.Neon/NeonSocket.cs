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
        }

        internal void ReceivedData(byte[] data)
        {
            if (this.DataReceived != null)
                this.DataReceived(this, new SocketReceivedDataEventArgs(data));
        }

        internal void SocketClosedByPeer()
        {
            if (SocketClosed != null)
            {
                SocketClosed(this, EventArgs.Empty);
            }
        }
    }

    public class SocketReceivedDataEventArgs : EventArgs
    {
        public SocketReceivedDataEventArgs(byte[] data)
        {
            this.Data = data;
        }

        public byte[] Data { get; private set; }
    }
}

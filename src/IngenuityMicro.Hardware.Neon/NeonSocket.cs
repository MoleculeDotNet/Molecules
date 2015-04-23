using System;
using Microsoft.SPOT;
using System.Text;
using IngenuityMicro.Net;

namespace IngenuityMicro.Hardware.Neon
{
    public class NeonSocket : ISocket, IDisposable
    {
        private readonly WifiDevice _parent;
        private readonly string _hostname;
        private readonly int _port;
        private readonly bool _fTcp;
        private int _iSocket;

        public event SocketReceivedDataEventHandler DataReceived;
        public event SocketClosedEventHandler SocketClosed;

        internal NeonSocket(WifiDevice device, int iSocket, string hostname, int port, bool fTcp)
        {
            _parent = device;
            _iSocket = iSocket;
            _hostname = hostname;
            _port = port;
            _fTcp = fTcp;
        }

        public string Hostname { get {  return _hostname; } }
        
        public int Port { get {  return _port; } }
        
        public bool UseTcp { get { return _fTcp; } }

        public void Dispose()
        {
            if (_iSocket != -1)
            {
                _parent.DeleteSocket(_iSocket);
                _iSocket = -1;
            }
        }

        public void Open()
        {
            if (_iSocket!=-1)
                _parent.OpenSocket(_iSocket);
        }

        public void Send(string payload)
        {
            Send(Encoding.UTF8.GetBytes(payload));
        }

        public void Send(byte[] payload)
        {
            if (_iSocket!=-1)
                _parent.SendPayload(_iSocket, payload);
        }

        public void Close()
        {
            if (_iSocket!=-1)
                _parent.CloseSocket(_iSocket);
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
}

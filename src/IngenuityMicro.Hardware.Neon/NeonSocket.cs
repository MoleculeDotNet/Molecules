using System;
using Microsoft.SPOT;
using System.Text;

namespace IngenuityMicro.Hardware.Neon
{
    public class NeonSocket
    {
        private readonly WifiDevice _parent;
        private readonly int _iSocket;

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
    }
}

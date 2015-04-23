using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Net
{
    public interface INetworkAdapter
    {
        ISocket OpenSocket(string hostNameOrAddress, int portNumber, bool useTcp);
    }
}

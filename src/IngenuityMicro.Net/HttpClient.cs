using System;
using System.Collections;
using System.Text;
using Microsoft.SPOT;

namespace IngenuityMicro.Net
{
    public class HttpClient : HttpBase, IDisposable
    {
        private readonly INetworkAdapter _adapter;
        private ISocket _socket;
        private HttpRequest _activeRequest;

        public HttpClient(INetworkAdapter adapter) : this(adapter, null, 80)
        {
        }

        public HttpClient(INetworkAdapter adapter, string host) : this(adapter, host, 80)
        {
        }

        public HttpClient(INetworkAdapter adapter, string host, int port)
        {
            _adapter = adapter;
            this.Host = host;
            this.Port = port;
            this.Headers.Add("Host",host);
        }

        public void Dispose()
        {
            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;                
            }
        }

        public HttpRequest CreateRequest()
        {
            return CreateRequest(HttpVerb.GET, "/");
        }

        public HttpRequest CreateRequest(string path)
        {
            return CreateRequest(HttpVerb.GET, path);
        }

        public HttpRequest CreateRequest(HttpVerb verb, string path)
        {
            string vstring;
            switch (verb)
            {
                case HttpVerb.GET:
                    vstring = "GET";
                    break;
                case HttpVerb.HEAD:
                    vstring = "HEAD";
                    break;
                case HttpVerb.POST:
                    vstring = "POST";
                    break;
                case HttpVerb.PUT:
                    vstring = "PUT";
                    break;
                case HttpVerb.DELETE:
                    vstring = "DELETE";
                    break;
                case HttpVerb.TRACE:
                    vstring = "TRACE";
                    break;
                case HttpVerb.OPTIONS:
                    vstring = "OPTIONS";
                    break;
                case HttpVerb.CONNECT:
                    vstring = "CONNECT";
                    break;
                case HttpVerb.PATCH:
                    vstring = "PATCH";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("verb","Unsupported verb");
            }
            return CreateRequest(vstring, path);
        }

        public HttpRequest CreateRequest(string verb, string path)
        {
            var result = new HttpRequest(this, verb, path);
            foreach (var key in this.Headers.Keys)
            {
                result.Headers[key] = this.Headers[key];
            }
            return result;
        }

        public string Host { get; private set; }

        public int Port { get; private set; }

        internal void SendRequest(HttpRequest req)
        {
            if (_activeRequest != null)
                throw new InvalidOperationException("A request is already outstanding");
            _activeRequest = req;

            EnsureSocketOpen();

            StringBuilder buffer = new StringBuilder();

            req.AppendMethod(buffer);
            req.AppendHeaders(buffer);
            req.AppendBody(buffer);

            _socket.Send(buffer.ToString());
        }

        private void EnsureSocketOpen()
        {
            try
            {
                if (_socket != null)
                    _socket.Open();
            }
            catch (Exception)
            {
                _socket = null;
            }
            if (_socket == null)
            {
                _socket = _adapter.OpenSocket(this.Host, this.Port, true);
                _socket.SocketClosed += SocketOnSocketClosed;
                _socket.DataReceived += SocketOnDataReceived;
            }
        }

        private void SocketOnDataReceived(object sender, SocketReceivedDataEventArgs args)
        {
            if (_activeRequest == null)
                return;

            if (_activeRequest.OnResponseReceived(args))
                _activeRequest = null;
        }

        private void SocketOnSocketClosed(object sender, EventArgs args)
        {
            // Nothing really to do here
        }
    }
}

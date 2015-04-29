using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Microsoft.SPOT;

namespace IngenuityMicro.Net
{
    public delegate void HttpResponseReceivedEventHandler(object sender, HttpResponse args);

    public class HttpRequest : HttpBase
    {
        private readonly HttpClient _client;
        private HttpResponse _response;
        private bool _failed = false;

        public event HttpResponseReceivedEventHandler ResponseReceived;

        internal HttpRequest(HttpClient client, string verb, string path)
        {
            _client = client;
            this.Verb = verb;
            this.Path = path;
        }

        /// <summary>
        /// Reset the state of the request so that the request can be re-sent.
        /// You can change the body and headers after reset and before resending.
        /// </summary>
        public void Reset()
        {
            // Prepare for a re-send of the same request
            _failed = false;
            _response = null;
        }

        public void Send()
        {
            _client.SendRequest(this);
        }

        internal bool OnResponseReceived(SocketReceivedDataEventArgs args)
        {
            // We got an unparsable reply, so ignore subsequent data packets
            if (_failed)
                return true;

            if (_response==null)
                _response = new HttpResponse();

            bool isComplete = false;
            try
            {
                isComplete = _response.ProcessResponse(args.Data);
            }
            catch (Exception)
            {
                _response = null;
                _failed = true;
                isComplete = true;
            }

            if (isComplete && this.ResponseReceived != null)
                this.ResponseReceived(this, _response);

            return isComplete;
        }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Verb { get; set; }

        public string Path { get; set; }

        internal void AppendMethod(StringBuilder buffer)
        {
            buffer.AppendLine(this.Verb + " " + this.Path + " HTTP/1.0");
        }

        internal void AppendHeaders(StringBuilder buffer)
        {
            foreach (var item in this.Headers)
            {
                var key = ((DictionaryEntry) item).Key;
                var val = ((DictionaryEntry) item).Value;

                //TODO: Dates and other types and well-known header keys may need special formatting
                // Content-Length is computed - don't allow an explicit value
                if (key.ToString() != "Content-Length")
                {
                    buffer.AppendLine(key + ": " + val);
                }
            }
            if (this.Body != null && this.Body.Length > 0)
            {
                buffer.AppendLine("Content-Length: " + this.Body.Length);
            }
            // terminate headers with a blank line
            buffer.Append("\r\n");
        }

        internal void AppendBody(StringBuilder buffer)
        {
            if (this.Body != null && this.Body.Length > 0)
            {
                buffer.Append(this.Body);
            }
        }
    }
}

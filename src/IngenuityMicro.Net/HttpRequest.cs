using System;
using System.Reflection;
using System.Text;
using Microsoft.SPOT;

namespace IngenuityMicro.Net
{
    public delegate void HttpResponseReceivedEventHandler(object sender, HttpResponse args);

    public class HttpRequest : HttpBase
    {
        private readonly HttpClient _client;

        public event HttpResponseReceivedEventHandler ResponseReceived;

        internal HttpRequest(HttpClient client, string verb, string path)
        {
            _client = client;
            this.Verb = verb;
            this.Path = path;
        }

        public void Send()
        {
            _client.SendRequest(this);
        }

        internal void OnResponseReceived(SocketReceivedDataEventArgs args)
        {
            string respText = null;
            try
            {
                // UTF8 conversion errors can cause a throw here
                respText = new string(Encoding.UTF8.GetChars(args.Data));
            }
            catch (Exception)
            {
                respText = null;
            }
            if (respText == null || respText.Length == 0)
                return;

            var response = new HttpResponse();
            try
            {
                response.ProcessResponse(respText);
            }
            catch (Exception)
            {
                response = null;
            }

            if (this.ResponseReceived != null)
                this.ResponseReceived(this, response);
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
            foreach (var key in this.Headers)
            {
                //TODO: Dates and other types and well-known header keys may need special formatting
                // Content-Length is computed - don't allow an explicit value
                if (key.ToString() != "Content-Length")
                {
                    buffer.AppendLine(key + ": " + this.Headers[key].ToString());
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

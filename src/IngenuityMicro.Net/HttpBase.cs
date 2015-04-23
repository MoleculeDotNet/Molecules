using System;
using Microsoft.SPOT;
using System.Collections;

namespace IngenuityMicro.Net
{
    public abstract class HttpBase
    {
        private readonly Hashtable _headers = new Hashtable();

        protected HttpBase()
        {
        }

        public string Username { get; set; }

        public string Password { get; set; }

        public Hashtable Headers { get { return _headers; } }

        public string Accept
        {
            get { return (string)_headers["Accept"]; }
            set { _headers["Accept"] = value; }
        }

        public string AcceptLanguage
        {
            get { return (string)_headers["Accept-Language"]; }
            set { _headers["Accept-Language"] = value; }
        }

        public string UserAgent
        {
            get { return (string)_headers["User-Agent"]; }
            set { _headers["User-Agent"] = value; }
        }
    }
}

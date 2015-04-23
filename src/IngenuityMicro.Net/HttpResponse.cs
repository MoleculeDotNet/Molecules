using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Net
{
    public class HttpResponse : HttpBase
    {
        internal HttpResponse()
        {
            this.Body = "";
            this.StatusCode = -1;
            this.Reason = "";
        }

        public int StatusCode { get; private set; }

        public string Reason { get; private set; }

        internal void ProcessResponse(string respText)
        {
            int section = 0;
            int start = 0;
            var idxNewline = respText.IndexOf('\n');
            bool fDone = false;
            while (idxNewline != -1)
            {
                var line = respText.Substring(start, idxNewline - start + 1);

                switch (section)
                {
                    case 0:
                        ProcessResultCode(line);
                        ++section;
                        break;
                    case 1:
                        if (ProcessHeader(line))
                            ++section;
                        break;
                    case 2:
                        // collect remaining text into the body
                        this.Body = respText.Substring(start);
                        fDone = true;
                        break;
                }
                if (fDone)
                    break;
                start = idxNewline + 1;
                idxNewline = respText.IndexOf('\n', start);
            }
        }

        private void ProcessResultCode(string line)
        {
            var tokens = line.Trim().Split(' ');
            if (tokens.Length > 1)
                this.StatusCode = int.Parse(tokens[1]);
            if (tokens.Length > 2)
                this.Reason = tokens[2];
        }

        private bool ProcessHeader(string line)
        {
            // End of input?
            line = line.Trim();
            if (line == "")
                return true;

            var idxColon = line.IndexOf(':');
            var key = line.Substring(0, idxColon).Trim();
            var value = line.Substring(idxColon + 1).Trim();
            this.Headers.Add(key,value);

            // Keep processing headers...
            return false;
        }
    }
}

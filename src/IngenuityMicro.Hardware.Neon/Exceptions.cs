using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Neon
{
    public class FailedExpectException : Exception
    {
        public FailedExpectException(string command, string expected, string actual)
#if MF_FRAMEWORK
            : base("Unexpected response to a command")
#else
            : base(string.Format("Command {0} expected {1} but received {2}", command, expected, actual))
#endif
        {
            this.Command = command;
            this.Expected = expected;
            this.Actual = actual;
        }

        public FailedExpectException(string expected, string actual)
#if MF_FRAMEWORK
            : base("Unexpected response to a command")
#else
            : base(string.Format("Expected {0} but received {1}", expected, actual))
#endif
        {
            this.Expected = expected;
            this.Actual = actual;
        }

        public string Command { get; private set; }
        public string Expected { get; private set; }
        public string Actual { get; private set; }
    }

    public class CommandTimeoutException : Exception
    {
        public CommandTimeoutException()
            : base("Timed out while waiting for a response from the device")
        {
            
        }

        public CommandTimeoutException(string command)
        {
            this.Command = command;
        }

        public string Command { get; private set; }
    }
}

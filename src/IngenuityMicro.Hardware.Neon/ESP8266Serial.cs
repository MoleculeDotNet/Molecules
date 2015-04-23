#define VERBOSE
using System;
using System.Collections;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Neon
{
    internal class ESP8266Serial
    {
        public delegate void DataReceivedHandler(object sender, byte[] stream, int channel);
        public delegate void SocketClosedHandler(object sender, int channel);

        public const int DefaultCommandTimeout = 10000;
        private readonly SerialPort _port;
        private readonly object _responseQueueLock = new object();
        private readonly ArrayList _responseQueue = new ArrayList();
        private readonly AutoResetEvent _responseReceived = new AutoResetEvent(false);
        private readonly object _lockSendExpect = new object();
        private readonly byte[] _ipdSequence;

        // Circular buffers that will grow in 256-byte increments - one for commands and one for received streams
        private readonly CircularBuffer _buffer = new CircularBuffer(256, 1, 256);
        private readonly CircularBuffer _stream = new CircularBuffer(256, 1, 256);

        public event DataReceivedHandler DataReceived;
        public event SocketClosedHandler SocketClosed;

        private int _cbStream = 0;
        private int _receivingOnChannel;
        private readonly ManualResetEvent _noStreamRead = new ManualResetEvent(true);

        public ESP8266Serial(SerialPort port)
        {
            this.CommandTimeout = DefaultCommandTimeout;
            _port = port;
            _ipdSequence = Encoding.UTF8.GetBytes("+IPD");
        }

        public void Start()
        {
            _port.DataReceived += PortOnDataReceived;
            _port.Open();
        }

        public int CommandTimeout { get; set; }

        public void SendCommand(string send)
        {
            lock (_lockSendExpect)
            {
                DiscardBufferedInput();
                WriteCommand(send);
            }
        }

        public void SendAndExpect(string send, string expect)
        {
            SendAndExpect(send, expect, DefaultCommandTimeout);
        }

        public void SendAndExpect(string send, string expect, int timeout)
        {
            lock (_lockSendExpect)
            {
                DiscardBufferedInput();
                WriteCommand(send);
                Expect(new[] { send }, expect, timeout);
            }
        }

        public string[] SendAndReadUntil(string send, string terminator)
        {
            return SendAndReadUntil(send, terminator, DefaultCommandTimeout);
        }

        public void Find(string successString)
        {
            Find(successString, DefaultCommandTimeout);
        }

        public void Find(string successString, int timeout)
        {
            SendAndReadUntil(null, successString, timeout);
        }

        public string[] SendAndReadUntil(string send, string terminator, int timeout)
        {
            ArrayList result = new ArrayList();
            if (send != null)
                SendCommand(send);
            do
            {
                var line = GetReplyWithTimeout(timeout);
                if (line != null && line.Length > 0)
                {
                    // in case echo is on
                    if (send != null && line.IndexOf(send) == 0)
                        continue;
                    // read until we see the magic termination string - usually 'OK'
                    if (line.IndexOf(terminator) == 0)
                        break;
                    result.Add(line);
                }
            } while (true);
            return (string[])result.ToArray(typeof(string));
        }

        public string SendCommandAndReadReply(string command, string replyPrefix)
        {
            return SendCommandAndReadReply(command, replyPrefix, DefaultCommandTimeout);
        }

        public string SendCommandAndReadReply(string command, string replyPrefix, int timeout)
        {
            var reply = SendCommandAndReadReply(command, DefaultCommandTimeout);
            if (replyPrefix != null)
            {
                if (reply.IndexOf(replyPrefix) != 0)
                    throw new FailedExpectException(command, replyPrefix, reply);
                reply = reply.Substring(replyPrefix.Length);
            }
            return reply;
        }

        public string SendCommandAndReadReply(string command)
        {
            return SendCommandAndReadReply(command, DefaultCommandTimeout);
        }

        public string SendCommandAndReadReply(string command, int timeout)
        {
            string response;
            lock (_lockSendExpect)
            {
                DiscardBufferedInput();
                WriteCommand(command);
                do
                {
                    response = GetReplyWithTimeout(timeout);
                } while (response == null || response == "" || response == command);
            }
            return response;
        }

        public void Expect(string expect)
        {
            Expect(null, expect, DefaultCommandTimeout);
        }

        public void Expect(string expect, int timeout)
        {
            Expect(null, expect, timeout);
        }

        public void Expect(string[] accept, string expect)
        {
            Expect(accept, expect, DefaultCommandTimeout);
        }

        public void Expect(string[] accept, string expect, int timeout)
        {
            if (accept == null)
                accept = new[] { "" };

            bool acceptableInputFound;
            string response;
            do
            {
                acceptableInputFound = false;
                response = GetReplyWithTimeout(timeout);

                foreach (var s in accept)
                {
#if MF_FRAMEWORK
                    if (response == "" || string.Equals(response.ToLower(), s.ToLower()))
#else
                    if (response=="" || string.Equals(response, s, StringComparison.OrdinalIgnoreCase))
#endif
                    {
                        acceptableInputFound = true;
                        break;
                    }
                }
            } while (acceptableInputFound);
#if MF_FRAMEWORK
            if (!string.Equals(response.ToLower(), expect.ToLower()))
#else
            if (!string.Equals(response, expect, StringComparison.OrdinalIgnoreCase))
#endif
            {
                throw new FailedExpectException(expect, response);
            }
        }

        public string GetReplyWithTimeout(int timeout)
        {
            string response = null;
            bool haveNewData;
            do
            {
                lock (_responseQueueLock)
                {
                    if (_responseQueue.Count > 0)
                    {
                        response = (string)_responseQueue[0];
                        _responseQueue.RemoveAt(0);
                    }
                    else
                    {
                        _responseReceived.Reset();
                    }
                }

                // If nothing was waiting in the queue, then wait for new data to arrive
                haveNewData = false;
                if (response == null)
                    haveNewData = _responseReceived.WaitOne(timeout, false);

            } while (response == null && haveNewData);

            // We have received no data, and the WaitOne timed out
            if (response == null && !haveNewData)
            {
                throw new CommandTimeoutException();
            }

            return response;
        }

        private byte[] ReadExistingBinary()
        {
            int arraySize = _port.BytesToRead;

            byte[] received = new byte[arraySize];

            _port.Read(received, 0, arraySize);

            return received;
        }

        /// <summary>
        /// Reads all immediately available bytes, based on the encoding, in both the stream and the input buffer of the SerialPort object.
        /// </summary>
        /// <returns>String</returns>
        private string ReadExisting()
        {
            try
            {
                return new string(Encoding.UTF8.GetChars(this.ReadExistingBinary()));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void DiscardBufferedInput()
        {
            // you cannot discard input if a stream read is in progress
            _noStreamRead.WaitOne();
            lock (_responseQueueLock)
            {
                _port.DiscardInBuffer();
                _responseQueue.Clear();
                _buffer.Clear();
                _stream.Clear();
                _responseReceived.Reset();
            }
        }

        public void Write(string txt)
        {
            this.Write(Encoding.UTF8.GetBytes(txt));
        }

        public void Write(byte[] payload)
        {
            _port.Write(payload, 0, payload.Length);
        }

        private void WriteCommand(string txt)
        {
#if VERBOSE
            Dbg("Sent: " + txt);
#endif
            this.Write(txt + "\r\n");
        }

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs serialDataReceivedEventArgs)
        {
            if (serialDataReceivedEventArgs.EventType == SerialData.Chars)
            {
                // keep doing this while there are bytes to read - don't rely on just even notification
                while (_port.BytesToRead > 0)
                {
                    while (_port.BytesToRead > 0)
                    {
                        var newInput = ReadExistingBinary();
                        if (newInput != null && newInput.Length > 0)
                        {
                            _buffer.Put(newInput);
                        }
                    }

                    // if we transitioned into a stream-reading mode with data still in the buffer, then loop
                    //   here until the buffer is drained or the stream is satisfied - whichever comes first.
                    do
                    {
                        // if _cbstream is non-zero, then we are reading a counted stream of bytes, not crlf-delimited input
                        if (_cbStream != 0)
                        {
                            // If we are capturing an input stream, then copy characters from the serial port
                            //   until the count of desired characters == 0
                            while (_cbStream > 0 && _buffer.Size > 0)
                            {
                                var eat = System.Math.Min(_buffer.Size, _cbStream);
                                _stream.Put(_buffer.Get(eat));
                                _cbStream -= eat;
                            }
                            // If we have fulfilled the stream request, then add the stream as a whole to the response queue
                            if (_cbStream == 0)
                            {
                                if (DataReceived != null)
                                {
                                    try
                                    {
                                        var data = _stream.Get(_stream.Size);
                                        _noStreamRead.Set();
                                        new Thread(() => { DataReceived(this, data, _receivingOnChannel); }).Start();
                                    }
                                    catch (Exception)
                                    {
                                        // mask exceptions in callback so that they don't kill our read loop
                                    }
                                }
                                _receivingOnChannel = -1;
                                _stream.Clear();
                            }
                        }

                        // process whatever is left in the buffer (after fulfilling any stream requests)
                        var idxNewline = _buffer.IndexOf(0x0A);
                        var idxIPD = _buffer.IndexOf(_ipdSequence);

                        while ((idxNewline != -1 || idxIPD != -1) && _cbStream == 0)
                        {
                            string line = "";
                            if (idxIPD == -1 || idxNewline < idxIPD)
                            {
                                line = ConvertToString(_buffer.Get(idxNewline));
                                // eat the newline too
                                _buffer.Skip(1);
                                if (line != null && line.Length > 0)
                                {
#if VERBOSE
                                    Dbg("Received Line : " + line);
#endif
                                    var idxClosed = line.IndexOf(",CLOSED");
                                    if (idxClosed != -1)
                                    {
                                        // Handle socket-closed notification
                                        var channel = int.Parse(line.Substring(0, idxClosed));
                                        if (this.SocketClosed != null)
                                            this.SocketClosed(this, channel);
                                    }
                                    else
                                        EnqueueLine(line);
                                }
                            }
                            else // idxIPD found before newline
                            {
                                // find the colon which ends the data-stream introducer
                                var idxColon = _buffer.IndexOf(0x3A);
                                // we did not get the full introducer - we have to wait for more chars to come in
                                if (idxColon == -1)
                                    break;
                                // Convert the introducer
                                _buffer.Skip(idxIPD);
                                line = ConvertToString(_buffer.Get(idxColon - idxIPD));
                                _buffer.Skip(1); // eat the colon

                                if (line != null && line.Length > 0)
                                {
                                    var tokens = line.Split(',');
                                    _receivingOnChannel = int.Parse(tokens[1]);
                                    _cbStream = int.Parse(tokens[2]);
                                    _noStreamRead.Reset();
#if VERBOSE
                                    Dbg("Reading a stream of " + _cbStream + " bytes for channel " + _receivingOnChannel);
#endif
                                }
                            }
                            // What next?
                            idxNewline = _buffer.IndexOf(0x0A);
                            idxIPD = _buffer.IndexOf(_ipdSequence);
                        }
                    } while (_cbStream > 0 && _buffer.Size > 0);
                }
            }
        }

        private string ConvertToString(byte[] input)
        {
            string result = null;
            try
            {
                result = new string(Encoding.UTF8.GetChars(input)).Trim();
            }
            catch (Exception)
            {
                // eat the exception and return null - conversion failures will manifest as an exception here
            }
            return result;
        }

        private void EnqueueLine(string line)
        {
            lock (_responseQueueLock)
            {
#if VERBOSE
                Dbg("Enqueue Line : " + line);
#endif
                _responseQueue.Add(line);
                _responseReceived.Set();
            }
        }

        [Conditional("DEBUG")]
        private static void Dbg(string msg)
        {
#if MF_FRAMEWORK
            Debug.Print(msg);
#else
            Debug.WriteLine(msg);
#endif
        }

    }
}

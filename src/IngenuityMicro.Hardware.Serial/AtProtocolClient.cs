//#define VERBOSE
using System;
using System.Collections;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Serial
{
    /// <summary>
    /// This delegate will be called whenever a unsolicited notification that you registered with AddUnsolicitedNotifications is received.
    /// </summary>
    /// <param name="sender">A reference to the AtProtocolClient that generated this call</param>
    /// <param name="line">The received line containing the notification</param>
    /// <param name="stream">If you will be starting a stream read, you can seed the stream by returning data here</param>
    /// <param name="cbStream">The count of stream bytes to be received. If you return a non-zero value here, the receive engine will begin a counted-bytes stream read</param>
    /// <param name="completionHandler">An optional handler to call on completion of a stream request. This is used only if stream is non null and/or cbStream is non-zero. If you do not
    /// provide a handler, then the stream content will be pushed into the response queue and the next Expect/Read will return the stream</param>
    /// <param name="context">A context object that will be passed back to the completion handler</param>
    public delegate void UnsolicitedNotificationEventHandler(
        object sender, ref string line, out string stream, out int cbStream,
        out StreamSatisfiedHandler completionHandler, out object context);

    /// <summary>
    /// If you respond to an UnsolicitedNotificationEvent by returning a completion handler, then this
    /// delegate will be called when the stream request has been satisfied (that is, all bytes received).
    /// </summary>
    /// <param name="sender">Ref to the AtProtocolClient</param>
    /// <param name="stream">The received data</param>
    /// <param name="context">Any context information that you returned from the UnsolicitedNotificationEventHandler</param>
    public delegate void StreamSatisfiedHandler(object sender, string stream, object context);

    /// <summary>
    /// Generalized client for interacting with AT-protocol hardware blocks like the ESP8266 and SIM800* (Adafruit Fona for instance). Handles various idiosyncracies among
    /// the different approaches to AT protocols and support counted-stream reading, even when embedded within a command line (e.g., ESP8266).
    /// </summary>
    public class AtProtocolClient
    {
        public const int DefaultCommandTimeout = 10000;

        private static Thread _eventDispatchThread = null;
        private static readonly object _eventQueueLock = new object();
        private static readonly ArrayList _eventQueue = new ArrayList();
        private static readonly AutoResetEvent _eventEnqueued = new AutoResetEvent(false);

        private readonly SerialPort _port;
        private readonly object _responseQueueLock = new object();
        private readonly ArrayList _responseQueue = new ArrayList();
        private readonly AutoResetEvent _responseReceived = new AutoResetEvent(false);
        private readonly object _lockSendExpect = new object();
        private readonly Hashtable _notifications = new Hashtable();
        private string _buffer;
        private StringBuilder _stream = new StringBuilder();
        private int _cbStream = 0;
        private object _streamContext;
        private StreamSatisfiedHandler _streamCompletionHandler;

        private class EventForDispatch
        {
            public object Sender;
            public object EventArgs;
        }

        public AtProtocolClient(SerialPort port)
        {
            this.CommandTimeout = DefaultCommandTimeout;

            _port = port;
        }

        public void Start()
        {
            _port.DataReceived += PortOnDataReceived;

            if (_eventDispatchThread == null)
            {
                _eventDispatchThread = new Thread(EventDispatcher);
                _eventDispatchThread.Start();
            }

            _port.Open();
        }

        public int CommandTimeout { get; set; }

        public void AddUnsolicitedNotifications(NotificationEntry[] notifications)
        {
            foreach (var item in notifications)
            {
                _notifications.Add(item.Notification, item.Handler);
            }
        }


        #region Sending Commands

        private string _newline = "\r\n";
        public string CommandNewLineSequence { get { return _newline; } set { _newline = value; } }

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
            if (send!=null)
                SendCommand(send);
            do
            {
                var line = GetReplyWithTimeout(timeout);
                if (line != null && line.Length > 0)
                {
                    // in case echo is on
                    if (send!=null && line.IndexOf(send) == 0)
                        continue;
                    // read until we see the magic termination string - usually 'OK'
                    if (line.IndexOf(terminator) == 0)
                        break;
                    result.Add(line);
                }
            } while (true);
            return (string[]) result.ToArray(typeof(string));
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

        public string[] SendCommandAndParseReply(string command, string replyPrefix, char delimiter)
        {
            return SendCommandAndParseReply(command, replyPrefix, delimiter, DefaultCommandTimeout);
        }

        public string[] SendCommandAndParseReply(string command, string replyPrefix, char delimiter, int timeout)
        {
            var reply = SendCommandAndReadReply(command, replyPrefix, timeout);
            return reply.Split(delimiter);
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
                } while (response == null || response == "" || response==command);
            }
            return response;
        }

        #endregion

        #region Parse responses

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
                accept = new[] {""};

            bool acceptableInputFound;
            string response;
            do
            {
                acceptableInputFound = false;
                response = GetReplyWithTimeout(timeout);

                foreach (var s in accept)
                {
#if MF_FRAMEWORK
                    if (response=="" || string.Equals(response.ToLower(), s.ToLower()))
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

            } while (response==null && haveNewData);

            // We have received no data, and the WaitOne timed out
            if (response == null && !haveNewData)
            {
                throw new CommandTimeoutException();
            }

            return response;
        }

        #endregion

        #region Serial Helpers

        private void PortOnDataReceived(object sender, SerialDataReceivedEventArgs serialDataReceivedEventArgs)
        {
            if (serialDataReceivedEventArgs.EventType == SerialData.Chars)
            {
                // keep doing this while there are bytes to read - don't rely on just even notification
                while (_port.BytesToRead > 0)
                {
                    string newInput = ReadExisting();
#if VERBOSE
                    //Dbg("ReadExisting : " + newInput);
#endif
                    if (newInput != null && newInput.Length > 0)
                    {
                        _buffer += newInput;

                        // if we transitioned into a stream-reading mode with data still in the buffer, then loop
                        //   here until the buffer is drained or the stream is satisfied - whichever comes first.
                        do
                        {
                            // if _cbstream is non-zero, then we are reading a counted stream of bytes, not crlf-delimited input
                            if (_cbStream != 0)
                            {
                                // If we are capturing an input stream, then copy characters from the serial port
                                //   until the count of desired characters == 0
                                while (_cbStream > 0 && _buffer.Length > 0)
                                {
                                    var eat = System.Math.Min(_buffer.Length, _cbStream);
                                    _stream.Append(_buffer.Substring(0, eat));
                                    _buffer = _buffer.Substring(eat);
                                    _cbStream -= eat;
                                }
                                // If we have fulfilled the stream request, then add the stream as a whole to the response queue
                                if (_cbStream == 0)
                                {
                                    if (_streamCompletionHandler != null)
                                    {
                                        try
                                        {
                                            _streamCompletionHandler(this, _stream.ToString(), _streamContext);
                                        }
                                        catch (Exception)
                                        {
                                            // mask exceptions in callback so that they don't kill our read loop
                                        }
                                    }
                                    else
                                    {
                                        EnqueueLine(_stream.ToString());
                                    }
                                    _streamCompletionHandler = null;
                                    _streamContext = null;
                                    _stream.Clear();
                                }
                            }

                            // process whatever is left in the buffer (after fulfilling any stream requests)
                            var idxNewline = _buffer.IndexOf('\n');
                            while (idxNewline != -1 && _cbStream == 0)
                            {
                                var line = _buffer.Substring(0, idxNewline);
                                _buffer = _buffer.Substring(idxNewline + 1);
                                while (line.Length > 0 && line[line.Length - 1] == '\r')
                                    line = line.Substring(0, line.Length - 1);
                                if (line.Length > 0)
                                {
#if VERBOSE
                                    Dbg("Received Line : " + line);
#endif
                                    // This routine can alter cbStream, buffer, and line resulting in
                                    //   some unhandled command input to be enqueued (whatever remains in 'line'
                                    //   after this call and also possibly throwing us into stream mode 
                                    //   in the middle of a command line (one of the less lovable aspects 
                                    //   of the ESP8266 protocol).
                                    HandleUnsolicitedResponses(ref line);
                                    if (line != null)
                                        EnqueueLine(line);
                                }

                                // See if we have another line buffered
                                idxNewline = _buffer.IndexOf('\n');
                            }
                        } while (_cbStream > 0 && _buffer.Length > 0);
                    }
                }
            }
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


        private void HandleUnsolicitedResponses(ref string line)
        {
            foreach (var item in _notifications.Keys)
            {
                if (line.IndexOf((string) item) == 0)
                {
                    var handler = (UnsolicitedNotificationEventHandler)_notifications[(string) item];
                    int cbStream;
                    string buffer;
                    handler(this, ref line, out buffer, out cbStream, out _streamCompletionHandler, out _streamContext);
                    if (cbStream != 0)
                    {
                        _cbStream = cbStream;
                        _stream.Clear();
                        if (buffer!=null)
                            _stream.Append(buffer);
                    }
                    if (_cbStream == 0 && buffer != null && buffer.Length > 0)
                    {
                        // The stream was immediately satisfied
                        if (_streamCompletionHandler != null)
                        {
                            try
                            {
                                _streamCompletionHandler(this, buffer, _streamContext);
                            }
                            catch (Exception)
                            {
                                // mask exceptions in callback so that they don't kill our read loop
                            }
                            _streamCompletionHandler = null;
                            _streamContext = null;
                        }
                        else // we got some stream input, but the caller did not provide a callback - push it into the output queue
                        {
                            // Do this here to preserve ordering
                            if (line != null && line.Length > 0)
                            {
                                EnqueueLine(line);
                                line = null;
                            }
                            EnqueueLine(buffer);
                        }
                    }
                    break;
                }
            }
        }

        private static void EventDispatcher()
        {
            while (true)
            {
                try
                {
                    object item;
                    do
                    {
                        item = null;
                        lock (_eventQueueLock)
                        {
                            if (_eventQueue.Count > 0)
                            {
                                item = _eventQueue[0];
                                _eventQueue.RemoveAt(0);
                            }
                        }
                        var eventForDispatch = item as EventForDispatch;
                        //TODO: Dispatch event
                    } while (item != null);
                    _eventEnqueued.WaitOne();
                }
                catch (Exception exc)
                {
                    // yes, catch everything - this thread has to keep plugging on
                    Dbg("An exception has occurred while dispatching events : " + exc);
                }
            }
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
            lock (_responseQueueLock)
            {
                _port.DiscardInBuffer();
                _responseQueue.Clear();
                _buffer = "";
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
            this.Write(txt + this.CommandNewLineSequence);
        }

        #endregion

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

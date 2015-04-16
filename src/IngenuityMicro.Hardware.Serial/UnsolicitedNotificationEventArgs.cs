using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Serial
{
    public class UnsolicitedNotificationEventArgs : EventArgs
    {
        public UnsolicitedNotificationEventArgs(string notification)
        {
            this.Notification = notification;
        }

        public string Notification { get; private set; }
    }
}

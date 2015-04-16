using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Serial
{
    public class NotificationEntry
    {
        public NotificationEntry(string notification, UnsolicitedNotificationEventHandler handler)
        {
            this.Notification = notification;
            this.Handler = handler;
        }

        public string Notification { get; private set; }
        public UnsolicitedNotificationEventHandler Handler { get; private set; }
    }
}

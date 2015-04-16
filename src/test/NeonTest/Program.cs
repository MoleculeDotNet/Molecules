using System;
using Microsoft.SPOT;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Neon;
using System.Threading;
using Microsoft.SPOT.Hardware;

namespace NeonTest
{
    public class Program
    {
        public static void Main()
        {
            var wifi = new WifiDevice();
            wifi.Booted += WifiOnBooted;
            wifi.Error += WifiOnError;
            wifi.ConnectionStateChanged += WifiOnConnectionStateChanged;
            bool state = true;
            while (true)
            {
                Hardware.UserLed.Write(state);
                state = !state;
                Thread.Sleep(500);
            }
        }

        private static void WifiOnConnectionStateChanged(object sender, EventArgs args)
        {
        }

        private static void WifiOnError(object sender, EventArgs args)
        {
        }

        private static void WifiOnBooted(object sender, EventArgs args)
        {
        }
    }
}

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
            wifi.Booted += WifiOnBooted;  // or you can wait on the wifi.IsInitializedEvent
            wifi.Error += WifiOnError;
            wifi.ConnectionStateChanged += WifiOnConnectionStateChanged;

            var apList = wifi.GetAccessPoints();
            Debug.Print("Access points:");
            foreach (var ap in apList)
            {
                Debug.Print("ECN : " + ap.Ecn);
                Debug.Print("SSID : " + ap.Ssid);
                Debug.Print("RSSI : " + ap.Rssi);
                Debug.Print("MAC addr : " + ap.MacAddress);
                Debug.Print("Connection is : " + (ap.AutomaticConnectionMode ? "Automatic" : "Manual"));
            }

            wifi.Connect("XXX","XXX");

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

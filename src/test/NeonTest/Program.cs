using System;
using System.Collections;
using System.Text;
using Microsoft.SPOT;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Neon;
using System.Threading;
using IngenuityMicro.Net;

namespace NeonTest
{
    public class Program
    {
        public static void Main()
        {
            var wifi = new NeonWifiDevice();
            wifi.EnableDebugOutput = true;
            wifi.EnableVerboseOutput = true;

            wifi.Booted += WifiOnBooted;  // or you can wait on the wifi.IsInitializedEvent
            //wifi.Error += WifiOnError;
            //wifi.ConnectionStateChanged += WifiOnConnectionStateChanged;

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

            wifi.Connect("CloudGate","Escal8shun");

            var sntp = new SntpClient(wifi, "time1.google.com");
            sntp.Start();

            var httpClient = new HttpClient(wifi, "www.example.com");
            var request = httpClient.CreateRequest();
            request.ResponseReceived += HttpResponseReceived;
            request.Send();

            bool state = true;
            int iCounter = 0;
            while (true)
            {
                Hardware.UserLed.Write(state);
                state = !state;
                if (++iCounter == 10)
                {
                    Debug.Print("Current UTC time : " + DateTime.UtcNow);
                    iCounter = 0;
                }
                Thread.Sleep(500);
            }
        }

        private static void HttpResponseReceived(object sender, HttpResponse resp)
        {
            if (resp == null)
            {
                Debug.Print("Failed to parse response");
                return;
            }
            Debug.Print("==== Response received ================================");
            Debug.Print("Status : " + resp.StatusCode);
            Debug.Print("Reason : " + resp.Reason);
            foreach (var item in resp.Headers)
            {
                var key = ((DictionaryEntry)item).Key;
                var val = ((DictionaryEntry)item).Value;
                Debug.Print(key + " : " + val);
            }
            if (resp.Body != null && resp.Body.Length > 0)
            {
                Debug.Print("Body:");
                Debug.Print(resp.Body);
            }
        }

        private static void WifiOnConnectionStateChanged(object sender, EventArgs args)
        {
            Debug.Print("Connection (association) state has changed");
        }

        private static void WifiOnError(object sender, EventArgs args)
        {
            Debug.Print("---- WIFI ERROR ----------------------");
        }

        private static void WifiOnBooted(object sender, EventArgs args)
        {
            Debug.Print("Wifi booted");
        }

    }
}

using System;
using System.Collections;
using System.Text;
using Microsoft.SPOT;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Neon;
using System.Threading;
using IngenuityMicro.Net;
using Microsoft.SPOT.Hardware;

namespace NeonTest
{
    public class Program
    {
        public static void Main()
        {
            var wifi = new WifiDevice();
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

            //var socket = wifi.OpenSocket("216.162.199.110", 80, true);
            //socket.DataReceived += (sender, args) =>
            //{
            //    Debug.Print("response received : " + new string(Encoding.UTF8.GetChars(args.Data)));
            //};
            //socket.SocketClosed += (sender, args) =>
            //{
            //    Debug.Print("Socket closed");
            //};
            //socket.Send("GET / HTTP/1.0\r\n\r\n");

            var httpClient = new HttpClient(wifi, "216.162.199.110");
            var request = httpClient.CreateRequest();
            request.ResponseReceived += HttpResponseReceived;
            request.Send();

            bool state = true;
            while (true)
            {
                Hardware.UserLed.Write(state);
                state = !state;
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
        }

        private static void WifiOnError(object sender, EventArgs args)
        {
        }

        private static void WifiOnBooted(object sender, EventArgs args)
        {
        }
    }
}

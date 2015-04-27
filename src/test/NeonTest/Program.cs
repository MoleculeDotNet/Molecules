using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Microsoft.SPOT;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Neon;
using System.Threading;
using IngenuityMicro.Net;
//using Microsoft.SPOT.Cryptoki;
using Microsoft.SPOT.Hardware;

namespace NeonTest
{
    public class Program
    {
        private static HashAlgorithm _hash = new HashAlgorithm(HashAlgorithmType.SHA256);

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

            //var httpClient = new HttpClient(wifi, "216.162.199.110");
            //var request = httpClient.CreateRequest();
            //request.ResponseReceived += HttpResponseReceived;
            //request.Send();

            // Azure test
            Debug.Print("DATE : " + CreateStorageKeyLite("foo"));

            //Convert.UseRFC4648Encoding = true;
            //var endpoint = "http://molecule.table.core.windows.net/";
            //var table = "readings";
            //var key = "6YgFBE1UKKRd7yoGtzeGvrtgFF9UbI5WvPIm/b4dhg1T7Z3IcQZnFoAYPmi4E2g+eo+otfuEp1fnODSLXoYN0Q==";

            //var httpClient = new HttpClient(wifi, "216.162.199.110");
            //var request = httpClient.CreateRequest(HttpVerb.POST, endpoint + table);
            //request.Headers.Add("Authorization","");



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

        private static string CreateStorageKeyLite(string resource)
        {
            return DateTime.UtcNow.ToString("r") + "\n" + resource;
        }

        private static string SignStorageKey(string key)
        {
            var hashBytes = _hash.ComputeHash(Encoding.UTF8.GetBytes(key));
            return Convert.ToBase64String(hashBytes);
        }
    }
}

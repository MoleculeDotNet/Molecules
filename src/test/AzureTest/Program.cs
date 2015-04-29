using System;
using System.Collections;
using System.Text;
using Microsoft.SPOT;
using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Neon;
using System.Threading;

using IngenuityMicro.Net;
using IngenuityMicro.Net.Azure.MobileService;

namespace AzureTest
{
    public class Program
    {
        private const string SSID = "XXX";
        private const string PASSWD = "XXX";
        private static readonly Uri ak16Uri = new Uri("http://ak16.azure-mobile.net/");
        private const string AzureAppKey = "XXX";

        public static void Main()
        {
            var wifi = new NeonWifiDevice();
            //wifi.EnableDebugOutput = true;
            //wifi.EnableVerboseOutput = true;

            wifi.Booted += WifiOnBooted;  // or you can wait on the wifi.IsInitializedEvent

            wifi.Connect(SSID, PASSWD);

            var sntp = new SntpClient(wifi, "time1.google.com");
            sntp.SetTime();

            MobileServiceClient msc = new MobileServiceClient(wifi, ak16Uri, null, AzureAppKey);
            IMobileServiceTable tab = msc.GetTable("environment_measures");

            //try
            //{
            // Test a dummy insert for now
            MeasurementRow row = new MeasurementRow();
            row.DateTimeStamp = DateTime.Now;
            row.where = "here"; //Resources.GetString(Resources.StringResources.VanWie);
            row.what = "nothing";
            row.value = -1f;
            row.uom = " ";
            string surprise = tab.Insert(row); // Failing on this line
            Debug.Print(DateTime.Now.ToString("T") + " " + surprise);

            //Uri uri = new Uri("http://www.example.com");
            //var httpClient = new HttpClient(wifi, uri.Host);
            //var request = new HttpRequest();
            //request.Uri = uri;
            //request.Headers.Add("Connection", "Keep-Alive");
            //request.ResponseReceived += HttpResponseReceived;
            //httpClient.SendAsync(request);

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

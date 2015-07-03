using System;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using IngenuityMicro.Hardware.Oxygen;
using IngenuityMicro.Hardware.Silicon;

namespace SiliconTest
{
    public class Program
    {
        public static void Main()
        {
            bool success = false;
            try
            {
                Debug.Print("Silicon Test Starting...");

                var fileSystem = new SiliconStorageDevice(Pin.PB9, SPI.SPI_module.SPI2);

                Debug.Print("Initializing");

                fileSystem.Initialize();
                if (!fileSystem.IsFormatted)
                {
                    Debug.Print("Formatting - go get lunch");
                    fileSystem.Format(); // takes quite awhile
                    Debug.Print("Formatting completed.");
                }
                fileSystem.Mount();

                var files = fileSystem.GetFiles();
                foreach (var file in files)
                {
                    Debug.Print("Deleting " + file);
                    fileSystem.Delete(file);
                }

                using (var stream = fileSystem.Open("SiliconTest.txt", FileMode.OpenOrCreate))
                {
                    using (var sw = new StreamWriter(stream))
                    {
                        sw.WriteLine("This is a test");
                    }
                }

                Debug.Print("Files:");
                files = fileSystem.GetFiles();
                foreach (var file in files)
                {
                    Debug.Print(file);
                }

                using (var stream = fileSystem.Open("SiliconTest.txt", FileMode.OpenOrCreate))
                {
                    using (var sw = new StreamReader(stream))
                    {
                        var contents = sw.ReadToEnd();
                        Debug.Print("Contents of test file : " + contents);
                    }
                }

                success = true;
            }
            catch
            {
                success = false;
            }

            int interval = 1;
            if (success)
                interval = 5;

            bool ledState = true;
            int iCounter = 0;
            while (true)
            {
                Hardware.UserLed.Write(ledState);
                if (++iCounter == interval)
                {
                    ledState = !ledState;
                    iCounter = 0;
                }
                Thread.Sleep(100);
            }
        }
    }
}

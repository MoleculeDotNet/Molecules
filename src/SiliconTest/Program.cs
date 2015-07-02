using System;
using Microsoft.SPOT;

using IngenuityMicro.Hardware.Silicon;
using System.IO;
using System.Text;
using System.Threading;

namespace SiliconTest
{
    public class Program
    {
        public static void Main()
        {
            Debug.Print("Silicon Test Starting...");

            var fileSystem = new SiliconStorageDevice();

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
                fileSystem.Delete(file);
            }

            var stream = fileSystem.Open("SiliconTest.txt", FileMode.OpenOrCreate);
            var buffer = Encoding.UTF8.GetBytes("This is a test");
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
            stream.Close();

            Debug.Print("Files:");
            files = fileSystem.GetFiles();
            foreach (var file in files)
            {
                Debug.Print(file);
            }

            Thread.Sleep(Timeout.Infinite);
        }
    }
}

using System;
using Microsoft.SPOT;
using System.IO;
using Microsoft.SPOT.Hardware;
using IngenuityMicro.Hardware.Oxygen;

namespace IngenuityMicro.Hardware.Silicon
{
    public class SiliconStorageDevice
    {
        private TinyFileSystem _tfs;

        public void Initialize()
        {
            var spiConfig = new SPI.Configuration(Pin.PA4, false, 0, 0, false, true, 12000, SPI.SPI_module.SPI1);
            var spi = new SPI(spiConfig);

            // Instantiate the block driver
            var driver = new MX25l3206BlockDriver(spi, Oxygen.Hardware.UserLed, 4);

            // Instantiate the file system passing the block driver for the underlying storage medium
            _tfs = new TinyFileSystem(driver);
        }

        public bool IsFormatted
        {
            get
            {
                return _tfs.CheckIfFormatted();
            }
        }

        public void Format()
        {
            _tfs.Format();
        }

        public void Mount()
        {
            _tfs.Mount();
        }

        public Stream Open(string fileName, FileMode fileMode)
        {
            return _tfs.Open(fileName, fileMode, 512);
        }

        public string[] GetFiles()
        {
            return _tfs.GetFiles();
        }

        public void Delete(string fileName)
        {
            _tfs.Delete(fileName);
        }

    }
}

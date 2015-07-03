using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;

namespace IngenuityMicro.Hardware.Silicon
{
    internal class FL164KIF01
    {
        private const byte CMD_GET_IDENTIFICATION = 0x9F;
        private const byte CMD_ERASE_SECTOR = 0x20;
        private const byte CMD_ERASE_BLOCK_4K = 0xD8;
        private const byte CMD_ERASE_BLOCK_8K = 0x40;
        private const byte CMD_ERASE_CHIP = 0xC7;
        private const byte CMD_WRITE_PAGE = 0x2;
        private const byte CMD_READ = 0x03;
        private const byte CMD_WRITE_ENABLE = 0x6;
        private const byte CMD_READ_STATUS = 0x5;

        public const int MaxAddress = 0x400000;
        public const int PageSize = 256;
        public const int SectoreSize = 4 * 2048;
        public const int BlockSize = 64 * 2048;

        private SPI _spi;
        private byte[] data1;
        private byte[] data2;
        private byte[] data4;
        private byte[] dataPage;

        private const byte ID_MANUFACTURE = 0xC2;
        private const byte ID_DEVICE_0 = 0x20;
        private const byte ID_DEVICE_1 = 0x16;

        private const byte DUMMY_BYTE = 0x00;

        public FL164KIF01(SPI spi)
        {
            _spi = spi;

            data1 = new byte[1];
            data2 = new byte[2];
            data4 = new byte[4];
            dataPage = new byte[PageSize + 4];
        }

        public bool WriteEnable()
        {
            data1[0] = CMD_WRITE_ENABLE;
            _spi.Write(data1);
            data1[0] = CMD_READ_STATUS;
            _spi.WriteRead(data1, data2);
            return ((data2[1] & 0x02) != 0);
        }

        public byte[] GetIdentification()
        {
            data1[0] = CMD_GET_IDENTIFICATION;
            _spi.WriteRead(data1, data4);

            if ((data4[1] == 0xFF && data4[2] == 0xFF && data4[3] == 0xFF) || (data4[1] == 0 && data4[2] == 0 && data4[3] == 0))
            {
                throw new Exception("Can not initialize flash");
            }

            return data4;
        }

        public bool WriteInProgress()
        {
            data1[0] = CMD_READ_STATUS;
            _spi.WriteRead(data1, data2);
            return ((data2[1] & 0x01) != 0);
        }

        public void EraseChip()
        {
            while (WriteEnable() == false) Thread.Sleep(0);
            data1[0] = CMD_ERASE_CHIP;
            _spi.Write(data1);
            while (WriteInProgress() == true) Thread.Sleep(0);
        }

        public bool EraseBlock(int block, int count)
        {
            if ((block + count) * BlockSize > MaxAddress)
            {
                throw new Exception("Invalid params");
            }

            int address = block * BlockSize;
            int i = 0;
            for (i = 0; i < count; i++)
            {
                while (WriteEnable() == false) Thread.Sleep(0);

                data4[0] = CMD_ERASE_BLOCK_4K;
                data4[1] = (byte)(address >> 16);
                data4[2] = (byte)(address >> 8);
                data4[3] = (byte)(address >> 0);
                _spi.Write(data4);
                address += BlockSize;

                while (WriteInProgress() == true) Thread.Sleep(0);
            }
            return i == count;
        }

        public bool EraseSector(int sector, int count)
        {
            if ((sector + count) * SectoreSize > MaxAddress) throw new ArgumentException("Invalid sector + count");

            int address = sector * SectoreSize;
            int i = 0;
            for (i = 0; i < count; i++)
            {
                while (WriteEnable() == false) Thread.Sleep(0);

                data4[0] = CMD_ERASE_SECTOR;
                data4[1] = (byte)(address >> 16);
                data4[2] = (byte)(address >> 8);
                data4[3] = (byte)(address >> 0);
                _spi.Write(data4);
                address += SectoreSize;

                while (WriteInProgress() == true) Thread.Sleep(0);
            }
            return i == count;
        }

        public bool WriteData(int address, byte[] array, int index, int count)
        {
            if ((array.Length - index) < count) throw new ArgumentException("Invalid index + count");
            if ((MaxAddress - count) < address) throw new ArgumentException("Invalid address + count");

            int block = count / PageSize;
            int length = count;
            int i = 0;
            if (block > 0)
            {
                for (i = 0; i < block; i++)
                {
                    while (WriteEnable() == false) Thread.Sleep(0);

                    dataPage[0] = CMD_WRITE_PAGE;
                    dataPage[1] = (byte)(address >> 16);
                    dataPage[2] = (byte)(address >> 8);
                    dataPage[3] = (byte)(address >> 0);
                    Array.Copy(array, index + (i * PageSize), dataPage, 4, PageSize);
                    _spi.Write(dataPage);

                    while (WriteInProgress() == true) Thread.Sleep(0);
                    address += PageSize;
                    length -= PageSize;
                }
            }

            if (length > 0)
            {
                while (WriteEnable() == false) Thread.Sleep(0);

                dataPage[0] = CMD_WRITE_PAGE;
                dataPage[1] = (byte)(address >> 16);
                dataPage[2] = (byte)(address >> 8);
                dataPage[3] = (byte)(address >> 0);
                Array.Copy(array, index + (i * PageSize), dataPage, 4, length);
                _spi.WriteRead(dataPage, 0, length + 4, null, 0, 0, 0);

                while (WriteInProgress() == true) Thread.Sleep(0);
                address += length;
                length -= length;
            }

            return length == 0;
        }

        public void ReadData(int address, byte[] array, int index, int count)
        {
            if ((array.Length - index) < count) throw new ArgumentException("Invalid index + count");
            if ((MaxAddress - count) < address) throw new ArgumentException("Invalid address + count");

            while (WriteEnable() == false) ;

            data4[0] = CMD_READ;
            data4[1] = (byte)(address >> 16);
            data4[2] = (byte)(address >> 8);
            data4[3] = (byte)(address >> 0);
            _spi.WriteRead(data4, 0, 4, array, index, count, 4);
        }
    }
}

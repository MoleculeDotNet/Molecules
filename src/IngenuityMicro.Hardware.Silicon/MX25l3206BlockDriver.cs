using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace IngenuityMicro.Hardware.Silicon
{
    public class MX25l3206BlockDriver : IBlockDriver
    {
        private MX25l3206 _flash;
        private ushort _clusterSize;
        private OutputPort _inUseIndicator;

        public MX25l3206BlockDriver(SPI spi, OutputPort inUseLed, int pagesPerCluster)
        {
            _flash = new MX25l3206(spi);
            _clusterSize = (ushort)(pagesPerCluster * MX25l3206.PageSize);
            _inUseIndicator = inUseLed;
        }

        public void Erase()
        {
            try
            {
                _inUseIndicator.Write(true);
                _flash.EraseChip();
            }
            finally
            {
                _inUseIndicator.Write(false);
            }
        }

        public void EraseSector(int sectorId)
        {
            try
            {
                _inUseIndicator.Write(true);
                _flash.EraseSector(sectorId, 1);
            }
            finally
            {
                _inUseIndicator.Write(false);
            }
        }

        public void Read(ushort clusterId, int clusterOffset, byte[] data, int index, int count)
        {
            try
            {
                _inUseIndicator.Write(true);
                int address = (clusterId * ClusterSize) + clusterOffset;
                _flash.ReadData(address, data, index, count);
            }
            finally
            {
                _inUseIndicator.Write(false);
            }
        }

        public void Write(ushort clusterId, int clusterOffset, byte[] data, int index, int count)
        {
            try
            {
                _inUseIndicator.Write(true);
                int address = (clusterId * ClusterSize) + clusterOffset;
                _flash.WriteData(address, data, index, count);
            }
            finally
            {
                _inUseIndicator.Write(false);
            }
        }

        public int DeviceSize
        {
            get { return MX25l3206.MaxAddress; }
        }

        public int SectorSize
        {
            get { return MX25l3206.SectoreSize; }
        }

        public ushort ClusterSize
        {
            get { return _clusterSize; }
        }
    }
}

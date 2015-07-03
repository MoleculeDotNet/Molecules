using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace IngenuityMicro.Hardware.Silicon
{
    public class FL164KIF01BlockDriver : IBlockDriver
    {
        private FL164KIF01 _flash;
        private ushort _clusterSize;
        private OutputPort _inUseIndicator;

        public FL164KIF01BlockDriver(SPI spi, OutputPort inUseLed, int pagesPerCluster)
        {
            _flash = new FL164KIF01(spi);
            _clusterSize = (ushort)(pagesPerCluster * FL164KIF01.PageSize);
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
            get { return FL164KIF01.MaxAddress; }
        }

        public int SectorSize
        {
            get { return FL164KIF01.SectoreSize; }
        }

        public ushort ClusterSize
        {
            get { return _clusterSize; }
        }
    }
}

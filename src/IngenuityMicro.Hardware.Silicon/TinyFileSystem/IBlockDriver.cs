using System;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// Interface to abstract the file system from the physical device.
  /// </summary>
  public interface IBlockDriver
  {
    /// <summary>
    /// Erases the entire device.
    /// </summary>
    void Erase();

    /// <summary>
    /// Erases a sector on the device.
    /// </summary>
    /// <param name="sectorId">Sector to be erased.</param>
    void EraseSector(int sectorId);

    /// <summary>
    /// Read a block of data from a cluster.
    /// </summary>
    /// <param name="clusterId">The cluster to read from.</param>
    /// <param name="clusterOffset">The offset into the cluster to start reading from.</param>
    /// <param name="data">The byte array to fill with the data read from the device.</param>
    /// <param name="index">The index into the array to start filling the data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    void Read(ushort clusterId, int clusterOffset, byte[] data, int index, int count);

    /// <summary>
    /// Write a block of data to a cluster.
    /// </summary>
    /// <param name="clusterId">The cluster to write to.</param>
    /// <param name="clusterOffset">The offset into the cluster to start writting to.</param>
    /// <param name="data">The byte array containing the data to be written.</param>
    /// <param name="index">The index into the array to start writting from</param>
    /// <param name="count">The number of bytes to write.</param>
    void Write(ushort clusterId, int clusterOffset, byte[] data, int index, int count);

    /// <summary>
    /// Full capacity of the device in bytes.
    /// </summary>
    int DeviceSize { get; }    

    /// <summary>
    /// The size in bytes of a sector on the device.
    /// </summary>    
    int SectorSize { get; }

    /// <summary>
    /// The cluster size in bytes.
    /// </summary>
    /// <remarks>
    /// Typically a flash devices have their sectors sub-divided into pages.
    /// A cluster size must be a multiple of the page size of the device, where
    /// a cluster overlays one or more pages.
    /// For example a device with a page size of 256 bytes can have cluster sizes
    /// of any multiple of 256 ie. 256, 512, 768, 1024...
    /// </remarks>
    ushort ClusterSize { get; }
  }
}

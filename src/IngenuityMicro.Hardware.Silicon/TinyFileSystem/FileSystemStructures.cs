using System;
using System.IO;
using System.Collections;
using System.Text;

namespace IngenuityMicro.Hardware.Silicon
{
  // The following summarizes the physical layout of the file system structures on the device.
  //
  // File Entry Cluster 
  // -------+-------+-----------------
  // offset | bytes | Description
  // -------+-------+-----------------  
  // 0      | 1     | Cluster Marker 
  //        |       |   1st Cluster of Sector : 0xff - unformatted/invalid sector, 0x7f - formatted sector/free cluster
  //        |       |   nth Cluster of Sector : 0xff - free cluster, 0x3f - allocated cluster
  //        |       |   0x1f - Orphaned page
  // 1      | 2     | Object ID > 0
  // 3      | 2     | Block Id  
  // 5      | 2     | Data Length
  // 7      | 1     | FileName Length
  // 8      | 16    | FileName
  // 24     | 8     | CreationTime
  // 32     | n     | The first n bytes of data contained in the file. The max value of n is dependent on the cluster size.

  // File Data Cluster
  // -------+-------+-----------------
  // offset | bytes | Description
  // -------+-------+-----------------  
  // 0      | 1     | Cluster Marker 
  //        |       |   1st Cluster of Sector : 0xff - unformatted/invalid sector, 0x7f - formatted sector/free cluster
  //        |       |   nth Cluster of Sector : 0xff - free cluster, 0x3f - allocated cluster
  //        |       |   0x1f - Orphaned page
  // 1      | 2     | Object ID = 0
  // 3      | 2     | Block Id  
  // 5      | 2     | Data Length
  // 7      | n     | n bytes of data contained in the file at Block Id. The max value of n is dependent on the cluster size.

  /// <summary>
  /// Markers used to indicate the state of the a cluster or sector on the disk
  /// In the case of a sector, this is just the first cluster on the disk which
  /// has the additional possible state of having the FormattedSector marker.
  /// </summary>
  static class BlockMarkers
  {
    public const byte ErasedSector = 0xff;
    public const byte FormattedSector = 0x7f;
    public const byte PendingCluster = 0x3f;
    public const byte AllocatedCluster = 0x1f;
    public const byte OrphanedCluster = 0x0f;

    public static byte[] FormattedSectorBytes = new byte[] { FormattedSector };
    public static byte[] PendingClusterBytes = new byte[] { PendingCluster };
    public static byte[] AllocatedClusterBytes = new byte[] { AllocatedCluster };
    public static byte[] OrphanedClusterBytes = new byte[] { OrphanedCluster };
  }

  /// <summary>
  /// In memory representation of a file on "disk".
  /// This structure tracks the files total size, the clusters 
  /// that make up the content of the file and the number of currently open
  /// Streams on the file.
  /// </summary>
  class FileRef
  {
    /// <summary>
    /// Unique object id of the file.
    /// </summary>
    public ushort ObjId;

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public int FileSize;

    /// <summary>
    /// Number of open streams on the file.
    /// </summary>
    public byte OpenCount;

    /// <summary>
    /// The list of clusters that make up the file content.
    ///
    /// Each block is sequenced in the order that the data occurs in the file.
    /// For example, index 0's value is the  cluster id of the disk location that 
    /// storing the data for the first block of file data, index 1 points to the 
    /// cluster id of the second block of data etc.
    /// <remarks>
    /// Block 0 also contains the files meta data in the form of a FileClusterBlock,
    /// subsequent block will be in the form of DataClusterBlocks. As a space optimization
    /// the FileClusterBlock also contain the initial data in the file.
    /// </remarks>
    /// </summary>
    public UInt16Array Blocks = new UInt16Array();
  }

  /// <summary>
  /// Statistics for the device
  /// </summary>
  public struct DeviceStats
  {
    /// <summary>
    /// Free memory available for use.
    /// </summary>
    /// <remarks>
    /// The free memory is reported in multiples of free cluster sizes. The unused space
    /// on currently allocated clusters is not reported. This also excludes any potential 
    /// free space that is currently occupied by orphaned clusters.
    /// </remarks>
    public readonly int BytesFree;

    /// <summary>
    /// Memory occupied by orphaned clusters.
    /// </summary>
    /// <remarks>
    /// This counter will report the amount of space currently allocated to orphaned clusters.
    /// Compacting the file system will return this memory to the free pool.
    /// </remarks>
    public readonly int BytesOrphaned;

    /// <summary>
    /// Creates an instance of the DeviceStats structure.
    /// </summary>
    /// <param name="bytesFree">Bytes free in the file system.</param>
    /// <param name="bytesOrphaned">Bytes orphaned in the file system.</param>
    public DeviceStats(int bytesFree, int bytesOrphaned)
    {
      BytesFree = bytesFree;
      BytesOrphaned = bytesOrphaned;
    }

    public override string ToString()
    {
      return "Bytes Free: " + BytesFree.ToString() + "\r\n" +
             "Bytes Orphaned: " + BytesOrphaned.ToString();
    }
  }

  /// <summary>
  /// Utility class used to serialize/deserialize the data structures of the file system
  /// between the in memory representation and the on device representation.
  /// </summary>
  /// <remarks>
  /// If these structures are changed you need to be very careful about correctly updating
  /// the constants found in this structure.
  /// </remarks>
  struct ClusterBuffer
  {
    public const int MaxFileNameLength = 16;
    public const int CommonHeaderSize = 1 + 2 + 2 + 2;
    public const int FileClusterHeaderSize = CommonHeaderSize + 1 + MaxFileNameLength + 8;
    public const int DataClusterHeaderSize = CommonHeaderSize;

    public const int MarkerOffset = 0;
    public const int ObjIdOffset = 1;
    public const int BlockIdOffset = 3;
    public const int DataLengthOffset = 5;
    public const int FileNameLengthOffset = 7;
    public const int FileNameOffset = 8;
    public const int CreationTimeOffset = 24;

    private byte[] _buffer;
    private int _clusterSize;
    private int _fileClusterMaxDataLength;
    private int _dataMaxDataLength;
    private int _minWrite;
    private int _maxWrite;

    public int MinWrite { get { return _minWrite; } }
    public int MaxWrite { get { return _maxWrite; } set { _maxWrite = value; } }
    public byte[] Buffer {get { return _buffer; } }
    public int FileClusterMaxDataLength { get { return _fileClusterMaxDataLength; } }
    public int DataClusterMaxDataLength { get { return _dataMaxDataLength; } }

    public ClusterBuffer(int clusterSize) : 
      this()
    {
      _clusterSize = clusterSize;
      _buffer = new byte[clusterSize];
      _fileClusterMaxDataLength = clusterSize - FileClusterHeaderSize;
      _dataMaxDataLength = clusterSize - DataClusterHeaderSize;
    }

    public void Clear()
    {
      Array.Clear(_buffer, 0, _clusterSize);
      _minWrite = 0;
      _maxWrite = 0;
    }
    

    #region Get methods
    public byte GetMarker()
    {
      return Blitter.GetByte(_buffer, MarkerOffset);
    }

    public ushort GetObjId()
    {
      return Blitter.GetUInt16(_buffer, ObjIdOffset);
    }

    public ushort GetBlockId()
    {
      return Blitter.GetUInt16(_buffer, BlockIdOffset);
    }

    public ushort GetDataLength()
    {
      return Blitter.GetUInt16(_buffer, DataLengthOffset);
    }

    public byte GetFileNameLength()
    {
      return Blitter.GetByte(_buffer, FileNameLengthOffset);
    }

    public string GetFileName()
    {
      byte byteCount = GetFileNameLength();
      return Blitter.GetString(_buffer, byteCount, FileNameOffset);
    }

    public DateTime GetCreationTime()
    {
      return Blitter.GetDateTime(_buffer, CreationTimeOffset);
    }    

    public ushort GetDataStartOffset()
    {
      return GetDataOffset(GetBlockId() == 0);
    }

    public ushort GetDataOffset(bool isFileEntry)
    {
      return (ushort)(isFileEntry ? FileClusterHeaderSize : DataClusterHeaderSize);
    }
    #endregion

    #region Set methods

    public void SetMarker(byte value)
    {
      UpdateWriteRange(MarkerOffset, Blitter.ToBytes(_buffer, value, MarkerOffset));            
    }

    public void SetObjId(ushort value)
    {      
      UpdateWriteRange(ObjIdOffset, Blitter.ToBytes(_buffer, value, ObjIdOffset));
    }

    public void SetBlockId(ushort value)
    {
      UpdateWriteRange(BlockIdOffset, Blitter.ToBytes(_buffer, value, BlockIdOffset));
    }

    public void SetDataLength(ushort value)
    {
      UpdateWriteRange(DataLengthOffset, Blitter.ToBytes(_buffer, value, DataLengthOffset));
    }

    public void SetFileName(string value)
    {
      byte byteLen = (byte)Blitter.ToBytes(_buffer, value.ToUpper(), MaxFileNameLength, FileNameOffset);
      Blitter.ToBytes(_buffer, byteLen, FileNameLengthOffset);
      UpdateWriteRange(FileNameOffset, byteLen);
    }

    public void SetCreationTime(DateTime value)
    {
      UpdateWriteRange(CreationTimeOffset, Blitter.ToBytes(_buffer, value, CreationTimeOffset));
    }

    public void SetData(byte[] data, int offset, int destinationOffset, int length)
    {
      int firstByteOffset = GetDataStartOffset() + destinationOffset;
      Array.Copy(data, offset, _buffer, firstByteOffset, length);
      UpdateWriteRange(firstByteOffset, length);
    }
    #endregion

    private void UpdateWriteRange(int offset, int length)
    {
      if (_minWrite > offset) _minWrite = offset;
      if (_maxWrite < offset + length) _maxWrite = offset + length;
    }

    public static implicit operator byte[](ClusterBuffer o)
    {
      return o._buffer;
    }
  }

  /// <summary>
  /// A utility class to facilitate the serialization and deserialization
  /// of the data types used in the FileS System structures to and from memory.
  /// </summary>
  /// <remarks>
  /// This utility class is limited to the types required by the file system.
  /// GetXXX  - functions extract the data type XXX from the supplied buffer starting at the specified index
  /// ToBytes - function push the byte representation of the data type into the supplied byte buffer starting at the specified index.
  /// </remarks>
  static class Blitter
  {
    #region GetXXX
    public static byte GetByte(byte[] buffer, int index)
    {
      return buffer[index];
    }

    public static ushort GetUInt16(byte[] buffer, int index)
    {
      byte b1 = buffer[index++];
      byte b2 = buffer[index];
      return (ushort)(b1 | (b2 << 8));
    }

    public static int GetInt32(byte[] buffer, int index)
    {
      byte b1 = buffer[index++];
      byte b2 = buffer[index++];
      byte b3 = buffer[index++];
      byte b4 = buffer[index];
      return (ushort)(b1 | (b2 << 8) | (b3 << 16) | (b4 | 24));
    }

    public static long GetInt64(byte[] buffer, int index)
    {
      long b1 = buffer[index++];
      long b2 = buffer[index++];
      long b3 = buffer[index++];
      long b4 = buffer[index++];
      long b5 = buffer[index++];
      long b6 = buffer[index++];
      long b7 = buffer[index++];
      long b8 = buffer[index];

      return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24) | (b5 << 32) | (b6 << 40) | (b7 << 48) | (b8 << 56);
    }

    public static DateTime GetDateTime(byte[] buffer, int index)
    {
      long ticks = GetInt64(buffer, index);
      return new DateTime(ticks);
    }

    public static string GetString(byte[] buffer, int index)
    {
      ushort length = GetUInt16(buffer, index);
      return GetString(buffer, length, index);
    }

    public static string GetString(byte[] buffer, int length, int index)
    {
      byte[] bytes = new byte[length];
      Array.Copy(buffer, index, bytes, 0, length);
      index += length;
      return new string(Encoding.UTF8.GetChars(bytes));
    }
    #endregion

    #region ToBytes
    public static int ToBytes(byte[] buffer, byte value, int index)
    {
      buffer[index] = value;
      return 1;
    }

    public static int ToBytes(byte[] buffer, ushort value, int index)
    {
      buffer[index++] = (byte)(value & 0xff);
      buffer[index] = (byte)((value >> 8) & 0xff);
      return 2;
    }

    public static int ToBytes(byte[] buffer, int value, int index)
    {
      buffer[index++] = (byte)(value & 0xff);
      buffer[index++] = (byte)((value >> 8) & 0xff);
      buffer[index++] = (byte)((value >> 16) & 0xff);
      buffer[index] = (byte)((value >> 24) & 0xff);
      return 4;
    }

    public static int ToBytes(byte[] buffer, long value, int index)
    {
      buffer[index++] = (byte)(value & 0xff);
      buffer[index++] = (byte)((value >> 8) & 0xff);
      buffer[index++] = (byte)((value >> 16) & 0xff);
      buffer[index++] = (byte)((value >> 24) & 0xff);
      buffer[index++] = (byte)((value >> 32) & 0xff);
      buffer[index++] = (byte)((value >> 40) & 0xff);
      buffer[index++] = (byte)((value >> 48) & 0xff);
      buffer[index] = (byte)((value >> 56) & 0xff);
      return 8;
    }

    public static int ToBytes(byte[] buffer, DateTime value, int index)
    {
      return ToBytes(buffer, value.Ticks, index);
    }

    public static int ToBytes(byte[] buffer, string value, int index)
    {      
      return ToBytes(buffer, value, ushort.MaxValue, index);
    }

    public static int ToBytes(byte[] buffer, string value, ushort maxLength, int index)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(value);
      int byteCount = bytes.Length;
      Array.Copy(bytes, 0, buffer, index, System.Math.Min(maxLength, byteCount));
      index += byteCount;
      return byteCount;
    }
    #endregion
  }
}
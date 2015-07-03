using System;
using Microsoft.SPOT;
using System.Text;
using System.Collections;
using System.IO;
using System.Threading;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// Tiny File System provides a basic file system which can be overlayed on any device providing
  /// a concrete implementation of the IBlockDriver interface.
  /// </summary>
  public class TinyFileSystem
  {
    #region Private state variables
    private IBlockDriver _blockDriver;
    private ushort _totalSectorCount;
    private ushort _clustersPerSector;
    private ushort _totalClusterCount;
    private ushort _lastObjId;
    private ushort _headSectorId;
    private ushort _tailClusterId;
    private ushort _freeClusterCount;    
    private ushort _minFreeClusters;

    private ushort _orphanedClusterCount;    
    private bool _mounted = false;
    private bool _compacting = false;
    
    private ClusterBuffer _cluster;
    private ClusterBuffer _defragBuffer;
    private byte[] _orphanedPerSector;

    private object _syncLock = new object();
//#if DEBUG
//    private ClusterBuffer _debugCluster;
//#endif

    private Hashtable _filesIndex = new Hashtable();

    #endregion

    #region .ctor
    /// <summary>
    /// Creates an instance of TinyFileSystem.
    /// </summary>
    /// <param name="blockDriver">Block driver which provides device access to the file system.</param>
    public TinyFileSystem(IBlockDriver blockDriver)
    {
      if (blockDriver == null) throw new ArgumentNullException("blockDriver");

      _blockDriver = blockDriver;

      // Precalculate commonly used values based on the device parameters provided by the block driver.
      _totalSectorCount = (ushort)(_blockDriver.DeviceSize / _blockDriver.SectorSize);
      _clustersPerSector = (ushort)(_blockDriver.SectorSize / _blockDriver.ClusterSize);
      _totalClusterCount = (ushort)(_blockDriver.DeviceSize / _blockDriver.ClusterSize);

      _orphanedPerSector = new byte[_totalSectorCount];

      // Initialize the device state tracking data
      _minFreeClusters = (ushort)(_clustersPerSector * 2);
      _headSectorId = 0;
      _tailClusterId = 0;

      // Precreate buffers that are used by the file system
      _cluster = new ClusterBuffer(_blockDriver.ClusterSize);
      _defragBuffer = new ClusterBuffer(_blockDriver.ClusterSize);

//#if DEBUG
//      _debugCluster = new ClusterBuffer(_blockDriver.ClusterSize);
//#endif
    }
    #endregion

    #region Public Tiny file system interface

    /// <summary>
    /// Scan the flash module to verify that it is formatted.
    /// </summary>
    /// <returns>true if formatted else false.</returns>
    public bool CheckIfFormatted()
    {
      for (ushort sectorId = 0; sectorId < _totalSectorCount; ++sectorId)
      {
        ushort clusterId = (ushort)(sectorId * _clustersPerSector);

        _blockDriver.Read(clusterId, 0, _cluster, 0, ClusterBuffer.CommonHeaderSize);
        byte marker = _cluster.GetMarker();

        if (marker != BlockMarkers.FormattedSector 
          && marker != BlockMarkers.AllocatedCluster 
          && marker != BlockMarkers.OrphanedCluster 
          && marker != BlockMarkers.PendingCluster)
        {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Mount an existing file system from a device.    
    /// </summary>
    /// <remarks>
    /// Before using the file system it must be mounted. The only exception is
    /// for Format, which will format the device and mount it immediately.
    /// 
    /// The user should ensure that the device has a valid file system.
    /// Currently there are only very basic heuristics to determine if the
    /// device has a valid file system. This will be improved over time.
    /// </remarks>
    public void Mount()
    {
      lock (_syncLock)
      {
        try
        {
          if (_mounted) return;

          Hashtable objIds = new Hashtable();

          int headClusterId = -1;
          int tailClusterId = -1;

          _freeClusterCount = 0;

          bool foundHole = false;
          bool foundData = false;
          bool isFirstClusterOfSector = false;

          for (ushort clusterId = 0; clusterId < _totalClusterCount; clusterId++)
          {
            _blockDriver.Read(clusterId, 0, _cluster, 0, ClusterBuffer.CommonHeaderSize);
            byte marker = _cluster.GetMarker();

            isFirstClusterOfSector = (clusterId % _clustersPerSector) == 0;

            if (isFirstClusterOfSector)
            {
              if (marker != BlockMarkers.FormattedSector
              && marker != BlockMarkers.AllocatedCluster
              && marker != BlockMarkers.OrphanedCluster
              && marker != BlockMarkers.PendingCluster)
              {
                throw new TinyFileSystemException(StringTable.Error_NotFormatted, TinyFileSystemException.TfsErrorCode.NotFormatted);
              }
            }

            bool clusterIsAvailable = marker == BlockMarkers.ErasedSector || marker == BlockMarkers.FormattedSector;
            bool clusterIsOrphaned = marker == BlockMarkers.OrphanedCluster || marker == BlockMarkers.PendingCluster;

            if (headClusterId != -1 && clusterIsAvailable) foundHole = true;
            if (tailClusterId != -1 && !clusterIsAvailable) foundData = true;

            if (!clusterIsAvailable && (headClusterId == -1 || foundHole))
            {
              headClusterId = clusterId;
              foundHole = false;
            }

            if (clusterIsAvailable && (tailClusterId == -1 || foundData))
            {
              tailClusterId = clusterId;
              foundData = false;
            }

            if (marker == BlockMarkers.AllocatedCluster)
            {
              ushort objId = _cluster.GetObjId();

              ushort blockId = _cluster.GetBlockId();
              if (blockId > _totalClusterCount) throw new TinyFileSystemException(StringTable.Error_NotFormatted, TinyFileSystemException.TfsErrorCode.NotFormatted); ;

              ushort length = _cluster.GetDataLength();
              if (length > _blockDriver.ClusterSize) throw new TinyFileSystemException(StringTable.Error_NotFormatted, TinyFileSystemException.TfsErrorCode.NotFormatted); ;

              if (objId > _lastObjId) _lastObjId = objId;

              FileRef file;
              if (!objIds.Contains(objId))
              {
                file = new FileRef();
                file.ObjId = objId;
                objIds[objId] = file;
              }
              else
              {
                file = (FileRef)objIds[objId];
              }

              file.Blocks[blockId] = clusterId;              
              file.FileSize += length;
            }
            else if (isFirstClusterOfSector && marker == BlockMarkers.FormattedSector)
            {
              _freeClusterCount += _clustersPerSector;
              clusterId += (ushort)(_clustersPerSector - 1);
              continue;
            }
            else if (clusterIsAvailable)
            {
              _freeClusterCount++;
            }
            else if (clusterIsOrphaned)
            {
              _orphanedClusterCount++;
              _orphanedPerSector[clusterId / _clustersPerSector]++;
            }
          }

          if (headClusterId != -1)
          {
            _headSectorId = (ushort)(headClusterId / _clustersPerSector);
          }
          else
          {
            _headSectorId = 0;
          }

          _tailClusterId = (ushort)(tailClusterId == -1 ? 0 : tailClusterId);

          foreach (FileRef file in objIds.Values)
          {
            if (file.Blocks.Count > 0)
            {
              _filesIndex.Add(file.ObjId, file);
            }
            else
            {
              for (int i = 0; i < file.Blocks.Count; i++)
              {
                MarkClusterOrphaned(file.Blocks[i]);
              }
            }
          }

          _mounted = true;
        }
        catch
        {
          // Something went wrong with the mount. Clean-up and re-throw.
          _filesIndex.Clear();
          throw;
        }
      }
    }

    /// <summary>
    /// Formats the device.
    /// </summary>
    /// <remarks>
    /// This will prepare the device for the file system. 
    /// Note that this will destroy any pre-existing data on the device.
    /// </remarks>
    public void Format()
    {
      lock (_syncLock)
      {
        // Check that there are no open files
        foreach (FileRef fr in _filesIndex.Values)
        {
          if (fr.OpenCount > 0) throw new IOException(StringTable.Error_FileIsInUse, (int)TinyFileSystemException.TfsErrorCode.FileInUse);
        }

        _blockDriver.Erase();

        for (ushort i = 0, clusterId = 0; i < _totalSectorCount; i++, clusterId += _clustersPerSector)
        {
          _blockDriver.Write(clusterId, 0, BlockMarkers.FormattedSectorBytes, 0, 1);
        }
        _headSectorId = 0;
        _tailClusterId = 0;
        _freeClusterCount = _totalClusterCount;
        _filesIndex.Clear();

        _mounted = true;
      }
    }

    /// <summary>
    /// Compacts the file system.
    /// </summary>
    /// <remarks>
    /// Collects all the active clusters and puts them together on the device and formats add the freed sectors.
    /// Frees up all the space occupied by inactive clusters.
    /// </remarks>
    public void Compact()
    {
      lock (_syncLock)
      {
        CheckState();
        _compacting = true;
        while (_orphanedClusterCount > 0)
        {
          // Find the best sector to migrate, the one that will reclaim the most free space          
          ushort sectorId = GetSectorToCompact();

          // Migrate the active data from the sector to tail of the log
          if (!MigrateSector(sectorId))
            break;

          // If it was not already the head sector, migrate the head sector into
          // the newly freed sector.
          if (sectorId != _headSectorId)
          {
            if (!MigrateSector(_headSectorId, (ushort)(sectorId * _clustersPerSector)))
              break;
          }

          // Move the head sector forward
          _headSectorId = (ushort)((_headSectorId + 1) % _totalSectorCount);          
        }
        _compacting = false;
      }
    }

    /// <summary>
    /// Copies a file to a new file.
    /// </summary>
    /// <param name="sourceFileName">The file to copy.</param>
    /// <param name="destFileName">The name of the destination file</param>
    /// <param name="overwrite">Specifies if the destination should be overwritten if it already exists. Default true.</param>
    public void Copy(string sourceFileName, string destFileName, bool overwrite = true)
    {
      lock (_syncLock)
      {
        CheckState();
        FileRef srcFile = GetFileRef(sourceFileName);
        if (srcFile == null) throw new IOException(StringTable.Error_FileNotFound, (int)IOException.IOExceptionErrorCode.FileNotFound);

        FileRef destFile = GetFileRef(destFileName);
        if (destFile != null)
        {
          if (overwrite)
          {
            Delete(destFile);
          }
          else
          {
            throw new IOException(StringTable.Error_FileAlreadyExists, (int)IOException.IOExceptionErrorCode.PathAlreadyExists);
          }
        }

        ushort newObjId = ++_lastObjId;

        _blockDriver.Read(srcFile.Blocks[0], 0, _cluster, 0, _blockDriver.ClusterSize);

        _cluster.SetMarker(BlockMarkers.PendingCluster);
        _cluster.SetFileName(destFileName);
        _cluster.SetCreationTime(DateTime.Now);
        _cluster.SetObjId(newObjId);

        ushort newClusterId = WriteToLog(_cluster, 0, _blockDriver.ClusterSize);
        MarkClusterAllocated(newClusterId);

        destFile = new FileRef();
        destFile.ObjId = newObjId;
        destFile.Blocks[0] = newClusterId;

        for (ushort blockId = 1; blockId < srcFile.Blocks.Count; blockId++)
        {
          _blockDriver.Read(srcFile.Blocks[blockId], 0, _cluster, 0, _blockDriver.ClusterSize);

          _cluster.SetMarker(BlockMarkers.PendingCluster);
          _cluster.SetObjId(destFile.ObjId);

          newClusterId = WriteToLog(_cluster, 0, _blockDriver.ClusterSize);
          MarkClusterAllocated(newClusterId);

          destFile.Blocks[blockId] = newClusterId;
        }

        destFile.FileSize = srcFile.FileSize;
        _filesIndex.Add(newObjId, destFile);
      }
    }

    /// <summary>
    /// Creates or overwrites a file.
    /// </summary>
    /// <param name="fileName">Name of the file to create.</param>
    /// <param name="bufferSize">Size of the read/write buffer. 0 for no buffering.</param>
    /// <returns>TinyFileStream that provides stream based access to the file.</returns>
    public Stream Create(string fileName, int bufferSize = 4096)
    {
      lock (_syncLock)
      {
        CheckState();
        var fs = CreateStream(fileName);
        return bufferSize > 0 ? new BufferedStream(fs, bufferSize) : (Stream)fs;
      }
    }

    /// <summary>
    /// Deletes a file from the device.
    /// </summary>
    /// <param name="fileName">Name of the file to delete.</param>
    /// <remarks>
    /// An IOException will be thrown if the file is open.
    /// </remarks>
    public void Delete(string fileName)
    {
      lock (_syncLock)
      {
        CheckState();
        FileRef file = GetFileRef(fileName);
        if (file == null) throw new IOException(StringTable.Error_FileNotFound, (int)IOException.IOExceptionErrorCode.FileNotFound);
        Delete(file);
      }
    }

    /// <summary>
    /// Determines if the specified file exists.
    /// </summary>
    /// <param name="fileName">Name of the file to check.</param>
    /// <returns>true if the file exists otherwise false.</returns>
    public bool Exists(string fileName)
    {
      lock (_syncLock)
      {
        CheckState();
        return GetFileRef(fileName) != null;
      }
    }

    /// <summary>
    /// Moves a file from the source to the destination.
    /// </summary>
    /// <remarks>
    /// Since the Tiny File System does not support directories, move is only used to rename a file.
    /// </remarks>
    /// <param name="sourceFileName">Name of the file to move.</param>
    /// <param name="destFileName">New name of the file.</param>
    public void Move(string sourceFileName, string destFileName)
    {
      lock (_syncLock)
      {
        CheckState();
        FileRef sourceFile = GetFileRef(sourceFileName);
        if (GetFileRef(destFileName) != null) throw new IOException(StringTable.Error_FileAlreadyExists, (int)IOException.IOExceptionErrorCode.PathAlreadyExists);

        ushort fileClusterId = sourceFile.Blocks[0];

        _blockDriver.Read(fileClusterId, 0, _cluster, 0, _blockDriver.ClusterSize);
        _cluster.SetMarker(BlockMarkers.PendingCluster);
        _cluster.SetFileName(destFileName);
        ushort newClusterId = WriteToLog(_cluster, 0, _blockDriver.ClusterSize);
        MarkClusterOrphaned(fileClusterId);
        MarkClusterAllocated(newClusterId);

        sourceFile.Blocks[0] = newClusterId;        
      }
    }

    /// <summary>
    /// Opens a TinyFileStream for the specified file.
    /// </summary>
    /// <param name="fileName">Name of the file to open.</param>
    /// <param name="fileMode">Specifies what to do when opening the file.</param>
    /// <param name="bufferSize">Size of the read/write buffer. 0 for no buffering.</param>
    /// <returns>A TinyFileStream which provides stream based access to the file.</returns>
    public Stream Open(string fileName, FileMode fileMode, int bufferSize = 4096)
    {
      lock (_syncLock)
      {
        CheckState();
        TinyFileStream fs = null;

        if (fileMode < FileMode.CreateNew || fileMode > FileMode.Append) throw new ArgumentOutOfRangeException("fileMode");

        FileRef file = GetFileRef(fileName);
        try
        {
          if (file != null) fs = new TinyFileStream(this, file);

          switch (fileMode)
          {
            case FileMode.Append:
              if (fs == null) fs = CreateStream(fileName);
              fs.Seek(0, SeekOrigin.End);
              break;

            case FileMode.Create:
              if (fs == null)
                fs = CreateStream(fileName);
              else
                Truncate(file, 0);
              break;

            case FileMode.CreateNew:
              if (fs != null) throw new IOException(StringTable.Error_FileAlreadyExists, (int)IOException.IOExceptionErrorCode.PathAlreadyExists);
              fs = CreateStream(fileName);
              break;

            case FileMode.Open:
              if (fs == null) throw new System.IO.IOException(StringTable.Error_FileNotFound, (int)IOException.IOExceptionErrorCode.FileNotFound);
              break;

            case FileMode.OpenOrCreate:
              if (fs == null) fs = CreateStream(fileName);
              break;

            case FileMode.Truncate:
              if (fs == null) throw new System.IO.IOException(StringTable.Error_FileNotFound);
              Truncate(file, 0);
              break;
          }

          return bufferSize > 0 ? new BufferedStream(fs, bufferSize) : (Stream)fs;
        }
        catch (Exception)
        {
          // Something failed so we need to clean-up and re-throw the exception
          if (fs != null) fs.Close();
          throw;
        }
      }
    }

    /// <summary>
    /// Opens a file, reads the content into a byte array and then closes the file.
    /// </summary>
    /// <param name="fileName">Name of the file to read.</param>
    /// <returns>A byte array containing the data from the file.</returns>
    public byte[] ReadAllBytes(string fileName)
    {
      lock (_syncLock)
      {
        CheckState();
        using (Stream fs = Open(fileName, FileMode.Open))
        {
          byte[] data = new byte[(int)fs.Length];
          fs.Read(data, 0, (int)fs.Length);
          return data;
        }
      }
    }

    /// <summary>
    /// Creates a new file, writes the byte array to the file and then closes it.
    /// The file is overwritten if it already exists.
    /// </summary>
    /// <param name="fileName">Name of the file to create.</param>
    /// <param name="data">Bytes to be written to the file.</param>
    public void WriteAllBytes(string fileName, byte[] data)
    {
      lock (_syncLock)
      {
        CheckState();
        using (Stream fs = Open(fileName, FileMode.Create))
        {
          fs.Write(data, 0, data.Length);
        }
      }
    }

    /// <summary>
    /// Returns the names of the files in the file system.
    /// </summary>
    /// <returns>An array of the names of the files.</returns>
    public string[] GetFiles()
    {
      lock (_syncLock)
      {
        CheckState();
        string[] files = new string[_filesIndex.Count];
        int i = 0;
        foreach (FileRef f in _filesIndex.Values)
        {
          files[i++] = GetFileName(f);
        }
        //Utilities.Sort(files);
        return files;
      }
    }

    /// <summary>
    /// Gets the size of the specified file.
    /// </summary>
    /// <param name="fileName">Name of the file for which to retrieve the file size.</param>
    /// <returns>Size of the file in bytes.</returns>
    public long GetFileSize(string fileName)
    {
      lock (_syncLock)
      {
        CheckState();
        FileRef file = GetFileRef(fileName);
        if (file == null) throw new System.IO.IOException(StringTable.Error_FileNotFound, (int)IOException.IOExceptionErrorCode.FileNotFound);
        return file.FileSize;
      }
    }

    /// <summary>
    /// Gets the creation time of the specified file.
    /// </summary>
    /// <param name="fileName">Name of the file for which to retrieve the creation time.</param>
    /// <returns>The creation time of the specified file.</returns>
    public DateTime GetFileCreationTime(string fileName)
    {
      lock (_syncLock)
      {
        CheckState();
        FileRef file = GetFileRef(fileName);
        if (file == null) throw new System.IO.IOException(StringTable.Error_FileNotFound, (int)IOException.IOExceptionErrorCode.FileNotFound);
        _blockDriver.Read(file.Blocks[0], 0, _cluster, 0, ClusterBuffer.FileClusterHeaderSize);
        return _cluster.GetCreationTime();
      }
    }

    /// <summary>
    /// Get the current statistics of the file system.
    /// </summary>
    /// <returns>Structure with the statistics of the file system.</returns>
    public DeviceStats GetStats()
    {
      lock (_syncLock)
      {
        return new DeviceStats(_freeClusterCount * _blockDriver.ClusterSize, _orphanedClusterCount * _blockDriver.ClusterSize);
      }
    }
    #endregion


    private void CheckState()
    {
      if (!_mounted) throw new InvalidOperationException(StringTable.Error_NotMounted);
    }

    private TinyFileStream CreateStream(string fileName)
    {      
      FileRef file = CreateFile(fileName);
      return new TinyFileStream(this, file);
    }

    private FileRef CreateFile(string fileName)
    {
      if (Encoding.UTF8.GetBytes(fileName).Length > ClusterBuffer.MaxFileNameLength) throw new ArgumentException("Filename exceeds 16 bytes", "filename");
        
      // Check for an existing file with the specified name.
      // If one exists, delete it.
      FileRef file = GetFileRef(fileName);
      if (file != null)
      {
        Delete(file);
      }

      // Generate the new object id for the new file
      ushort newObjId = ++_lastObjId;

      // Fill the custer buffer with the FileCluster data.
      // The cluster is initially marked as pending.
      _cluster.Clear();
      _cluster.SetMarker(BlockMarkers.PendingCluster);
      _cluster.SetObjId(newObjId);
      _cluster.SetBlockId(0);
      _cluster.SetFileName(fileName);
      _cluster.SetCreationTime(DateTime.Now);

      // Create the FileRef
      file = new FileRef() { ObjId = newObjId, FileSize = 0 };

      // Write the FileCluster to the log an get the cluster id
      ushort newClusterId = WriteToLog(_cluster, 0, _cluster.MaxWrite);

      // Mark the cluster as Allocated.
      MarkClusterAllocated(newClusterId);

      // Update the block 0 entry to reference the cluster on the device
      // containing the FileCluster entry for the new file.
      file.Blocks[0] = newClusterId;

      // Add the file to the in-memory index
      _filesIndex.Add(file.ObjId, file);

      return file;
    }

    internal int Read(FileRef file, long filePosition, byte[] data, int offset, int length)
    {
      lock (_syncLock)
      {
        // Reads at or past the end of the file returns 0.
        if (filePosition >= file.FileSize) return 0;

        ushort firstBlockId;
        ushort dataOffset;

        // Dermine if we in the special case of reading data from the first cluster
        // of the file. This data is read from the FileCluster.
        if (filePosition < _cluster.FileClusterMaxDataLength)
        {
          firstBlockId = 0;
          dataOffset = (ushort)filePosition;
        }
        else
        {
          // Not reading from the first cluster of the file.
          // Compensate for the asymetric data size between FileClusters and DataClusters.
          // This requires adjusting the offset into the cluster from where the data is read from.
          long adjustedOffset = filePosition - _cluster.FileClusterMaxDataLength;
          firstBlockId = (ushort)((adjustedOffset / _cluster.DataClusterMaxDataLength) + 1);
          dataOffset = (ushort)(adjustedOffset % _cluster.DataClusterMaxDataLength);
        }

        // Track the number of bytes remaining to be read
        int bytesRemaining = length;

        // Loop through each block of the file reading the data and packing it into the provided
        // byte array until there are either no more blocks to read or bytesRemaining is 0.
        for (int blockId = firstBlockId; bytesRemaining > 0 && blockId < file.Blocks.Count; blockId++)
        {
          _cluster.Clear();

          // Get the cluster id for the file block
          ushort clusterId = file.Blocks[blockId];

          // Read the cluster into memory
          _blockDriver.Read(clusterId, 0, _cluster, 0, _blockDriver.ClusterSize);

          // Calculate how much data we have from this cluster
          ushort dataLength = _cluster.GetDataLength();
          ushort bytesToRead = (ushort)System.Math.Min(bytesRemaining, dataLength - dataOffset);

          // Transfer the data from the cluster buffer to the data array
          Array.Copy(_cluster, _cluster.GetDataStartOffset() + dataOffset, data, offset, bytesToRead);

          // Update the tracking variables.
          dataOffset = 0;
          offset += bytesToRead;
          bytesRemaining -= bytesToRead;
        }

        // Return the final number of bytes read
        return length - bytesRemaining;
      }
    }

    internal void Write(FileRef file, long filePosition, byte[] data, int srcOffset, int length)
    {      
      lock (_syncLock)
      {
        if (filePosition > file.FileSize) throw new IOException(StringTable.Error_WritePastEnd, (int)IOException.IOExceptionErrorCode.Others);

        ushort firstBlockId;
        ushort clusterDataOffset;

        // Dermine if we in the special case of writing data to the first cluster
        // of the file. This data is written to the FileCluster entry.
        if (filePosition < _cluster.FileClusterMaxDataLength)
        {
          firstBlockId = 0;
          clusterDataOffset = (ushort)filePosition;
        }
        else
        {
          // Not writing to the first cluster of the file.
          // Compensate for the asymetric data size between FileClusters and DataClusters.
          // This requires adjusting the offset into the cluster to where the data will be written. 
          long adjustedOffset = filePosition - _cluster.FileClusterMaxDataLength;
          firstBlockId = (ushort)((adjustedOffset / _cluster.DataClusterMaxDataLength) + 1);
          clusterDataOffset = (ushort)(adjustedOffset % _cluster.DataClusterMaxDataLength);
        }

        // Track the number of byte remaining to be written.
        int bytesRemaining = length;

        // Loop through each block of the file writting the data from the data buffer to the 
        // cluster.
        for (ushort blockId = firstBlockId; bytesRemaining > 0; blockId++)
        {
          _cluster.Clear();

          // Calculate how much data can be written to the target cluster. If the cluster is a block 0
          // cluster (first block of the file) the max data that can be written to the cluster is 
          // _cluster.FileClusterMaxDataLength otherwise it is _cluster.DataClusterMaxDataLength.
          // the actual values depends on the cluster size supported by the IBlockDriver.
          int maxDataLength = blockId == 0 ? _cluster.FileClusterMaxDataLength : _cluster.DataClusterMaxDataLength;

          // Calculate the number of bytes to write from the the data buffer to the cluster.
          ushort bytesToWrite = (ushort)System.Math.Min(bytesRemaining, maxDataLength - clusterDataOffset);

          int currentSize = 0;
          int sizeDelta = 0;
          ushort orphanedClusterId;
          ushort newClusterId;

          // Check if we are writting to an existing block of the file or is the
          // file gaining a new block.
          if (blockId < file.Blocks.Count)
          {
            // We are writting to an existing block.          
            // The cluster from the existing block will be orphaned and a new replacement cluster
            // created with the modified data.

            // Get the cluster id that will be orphaned
            orphanedClusterId = file.Blocks[blockId];

            ushort clusterReadOffset;
            // Update the _cluster.MaxWrite based on the type of cluster we are writting to.
            // First cluster is a FileCluster subsequent clusters are DataCluster entries.
            if (blockId == 0)
            { 
              // Read the cluster header
              _blockDriver.Read((ushort)orphanedClusterId, 0, _cluster, 0, ClusterBuffer.FileClusterHeaderSize);
              clusterReadOffset = ClusterBuffer.FileClusterHeaderSize;

              if (file.FileSize > 0)
              {
                currentSize = _cluster.GetDataLength();
              }
              else
              {
                currentSize = 0;
              }
              _cluster.MaxWrite = ClusterBuffer.FileClusterHeaderSize + currentSize;
            }
            else
            {
              // Read the cluster header
              _blockDriver.Read((ushort)orphanedClusterId, 0, _cluster, 0, ClusterBuffer.DataClusterHeaderSize);
              clusterReadOffset = ClusterBuffer.DataClusterHeaderSize;

              currentSize = _cluster.GetDataLength(); ;
              _cluster.MaxWrite = ClusterBuffer.DataClusterHeaderSize + currentSize;
            }

            // Read the remainder of the cluster
            if (currentSize > 0)
            {
              _blockDriver.Read((ushort)orphanedClusterId, clusterReadOffset, _cluster, clusterReadOffset, currentSize);
            }

            // Calculate if the amount of data on the cluster is growing or are we just
            // overwritting existing data on the cluster.
            int excess = (clusterDataOffset + bytesToWrite) - currentSize;
            if (excess > 0)
            {
              sizeDelta = excess;
            }
            _cluster.MaxWrite = clusterReadOffset + currentSize + sizeDelta;

            // Setup the cluster buffer for the new cluster that will be written
            // the cluster is initially written as a Pending Cluster.
            // Note: the cluster buffer already contains the objid and blockid from the inital
            //       read from the device.
            _cluster.SetMarker(BlockMarkers.PendingCluster);
            _cluster.SetDataLength((ushort)(currentSize + sizeDelta));
            _cluster.SetData(data, srcOffset, clusterDataOffset, bytesToWrite);

            // Write the new cluster to the log and get the cluster id.
            newClusterId = WriteToLog(_cluster, 0, _cluster.MaxWrite);

            // Mark the old cluster as orphaned
            MarkClusterOrphaned((ushort)orphanedClusterId);

            // Mark the new cluster as allocated (active part of the file).
            MarkClusterAllocated(newClusterId);
          }
          else
          {
            // We are writting a cluster for the file, so the file will gain a new block.

            sizeDelta = bytesToWrite;

            // Setup the cluster buffer with the relevant data or the new block.
            // The cluster is initialy written as a Pending cluster.
            _cluster.SetMarker(BlockMarkers.PendingCluster);
            _cluster.SetObjId(file.ObjId);
            _cluster.SetBlockId(blockId);

            _cluster.SetDataLength((ushort)(currentSize + sizeDelta));
            _cluster.SetData(data, srcOffset, clusterDataOffset, bytesToWrite);

            // Write the cluster to the log and get the cluster id.
            newClusterId = WriteToLog(_cluster, 0, _cluster.MaxWrite);

            // Mark the cluster as Allocated
            MarkClusterAllocated(newClusterId);
          }

          // Update the in-memory file entry with the new cluster id for the file block
          // that was written to. And update the in file size.
          file.Blocks[blockId] = newClusterId;
          file.FileSize += sizeDelta;

          bytesRemaining -= bytesToWrite;
          srcOffset += bytesToWrite;
          clusterDataOffset = 0;
        }
      }
    }

    internal void Truncate(FileRef file, long filePosition)
    {
      lock (_syncLock)
      {
        if (filePosition > file.FileSize) throw new IOException(StringTable.Error_WritePastEnd, (int)IOException.IOExceptionErrorCode.Others);

        ushort firstBlockId;
        ushort dataOffset;

        // Determine if we are truncating the file at a location within 
        // the first block of the file or one of the later blocks.
        if (filePosition < _cluster.FileClusterMaxDataLength)
        {
          firstBlockId = 0;
          dataOffset = (ushort)filePosition;
        }
        else
        {
          // If it is not the first block of the file we need to compensate for the 
          // asymetric data size written to the File Cluster vs. that of the subsequent
          // Data Cluster(s).
          long adjustedOffset = filePosition - _cluster.FileClusterMaxDataLength;
          firstBlockId = (ushort)((adjustedOffset / _cluster.DataClusterMaxDataLength) + 1);
          dataOffset = (ushort)(adjustedOffset % _cluster.DataClusterMaxDataLength);
        }

        if (dataOffset > 0 || firstBlockId == 0)
        {
          // We are truncating the file starting at a offset into the cluster
          // so we need to write the earlier data to a new cluster and orphan the 
          // old cluster.

          // Store the current cluster id of the block.
          ushort orphanedClusterId = file.Blocks[firstBlockId];

          // Read the current cluster data into the cluster buffer.
          _blockDriver.Read(file.Blocks[firstBlockId], 0, _cluster, 0, _blockDriver.ClusterSize);

          // Update the cluster buffer to prepare it for the new cluster.
          // The new cluster will be written as a pending cluster.
          _cluster.SetMarker(BlockMarkers.PendingCluster);
          _cluster.SetDataLength(dataOffset);
          _cluster.MaxWrite = _cluster.GetDataStartOffset() + dataOffset;

          // Write the cluster to the log and get the new cluster id
          ushort newClusterId = WriteToLog(_cluster, 0, _cluster.MaxWrite);

          // Mark the old cluster as orphaned
          MarkClusterOrphaned(orphanedClusterId);

          // Mark the new cluster as the allocated cluster
          MarkClusterAllocated(newClusterId);

          // Update the in-memory file reference of the file block to point
          // to the new cluster.
          file.Blocks[firstBlockId] = newClusterId;

          // Update what we consider the first block to be dropped.
          // The current first block has already had it's data truncated.
          firstBlockId++;
        }

        // Loop through all the blocks of the file starting with the calculated first block
        // and mark each cluster as orphaned.
        for (int blockId = firstBlockId; blockId < file.Blocks.Count; blockId++)
        {
          MarkClusterOrphaned(file.Blocks[blockId]);
        }

        // Remove the unused blocks from the in memory file reference.
        file.Blocks.SetLength(firstBlockId);

        // Update the file size to reflect the new truncated size.
        file.FileSize = (int)filePosition;
      }
    }

    private void Delete(FileRef file)
    {
      if (file.OpenCount > 0) throw new IOException(StringTable.Error_FileIsInUse, (int)TinyFileSystemException.TfsErrorCode.FileInUse);

      try
      {
        // Loop through each block of the file and mark the
        // referenced clusters as deleted.
        for (int i = 0; i < file.Blocks.Count; i++)
        {
          MarkClusterOrphaned(file.Blocks[i]);
        }
      }
      finally
      {
        // Remove the file from the in memory index.
        _filesIndex.Remove(file.ObjId);
      }
    }

    private FileRef GetFileRef(string fileName)
    { 
      // Retrieve a FileRef from the file index based
      // on the name of the file.
      string nameToSearch = fileName.ToUpper();
      foreach (FileRef f in _filesIndex.Values)
      {
        if (nameToSearch == GetFileName(f))
        {
          return f;
        }        
      }
      return null;
    }

    private string GetFileName(FileRef file)
    {
      // Extract the file name from the first block of the file
      // which is a File Cluster.
      ushort clusterId = file.Blocks[0];
      _blockDriver.Read(clusterId, ClusterBuffer.FileNameLengthOffset, _cluster, ClusterBuffer.FileNameLengthOffset, 2 + ClusterBuffer.MaxFileNameLength);
      return _cluster.GetFileName();
    }

    private ushort WriteToLog(byte[] data, int index, int count)
    {
      // Check if we need to perform a compaction to free up some space to write the new log entry      
      if (!_compacting && _freeClusterCount <= _minFreeClusters) 
        PartialCompact();
      
      if (!_compacting && _freeClusterCount <= _minFreeClusters) 
        throw new TinyFileSystemException(StringTable.Error_DiskFull, TinyFileSystemException.TfsErrorCode.DiskFull);

      ushort clusterId = WriteToLogInternal(data, index, count);
      
      _freeClusterCount--;

      return clusterId;
    }

    private ushort WriteToLogInternal(byte[] data, int index, int count)
    {
      // store the current tail cluster id
      ushort clusterId = _tailClusterId;

      WriteToCluster(clusterId, data, index, count);

      // Move the tail forward wrapping around to the begining once we reach the end
      // of the device.
      _tailClusterId = (ushort)((_tailClusterId + 1) % _totalClusterCount);

      // return the cluster id that was written to.
      return clusterId;
    }
    
    private void WriteToCluster(ushort clusterId, byte[] data, int index, int count)
    {
//#if DEBUG
//      _blockDriver.Read(clusterId, 0, _debugCluster, 0, _blockDriver.ClusterSize);
//      int marker = _debugCluster.GetMarker();
//      Debug.Assert(marker == BlockMarkers.FormattedSector || marker == BlockMarkers.ErasedSector);
//#endif
      // Write the data to the tail of the log
      _blockDriver.Write(clusterId, 0, data, index, count);
    }

    private void PartialCompact()
    {
      DateTime starTime = DateTime.Now;
      try
      {
        _compacting = true;

        // While there are enough orphaned clusters to make compaction worth while and we
        // have less than the threshold of free clusters, keep compacting the sectors.
        while (_freeClusterCount <= _minFreeClusters && _orphanedClusterCount >= _clustersPerSector)
        {
          // Find the best sector to migrate, the one that will reclaim the most free space          
          ushort sectorId = GetSectorToCompact();

          // Migrate the active data from the sector to tail of the log
          if (!MigrateSector(sectorId)) 
            break;

          // If it was not already the head sector, migrate the head sector into
          // the newly freed sector.
          if (sectorId != _headSectorId)
          {
            if (!MigrateSector(_headSectorId, (ushort)(sectorId * _clustersPerSector))) 
              break;
          }

          // Move the head sector forward
          _headSectorId = (ushort)((_headSectorId + 1) % _totalSectorCount);
        }                
      }
      finally
      {
//#if DEBUG
//        Debug.Print("Partial Compact: " + ((DateTime.Now - starTime).Ticks / TimeSpan.TicksPerSecond).ToString());
//#endif
        _compacting = false;
      }
    }

    private ushort GetSectorToCompact()
    {
      // If the head sector has any orphaned data then we should migrate it directly
      if (_orphanedPerSector[_headSectorId] > 0) return _headSectorId;

      // Locate the sector with the most orphaned data
      ushort bestSector = _headSectorId;
      ushort tailSector = (ushort)(_tailClusterId / _clustersPerSector);
      byte mostOrphaned = 0;
      for (ushort i = 0; i < _totalSectorCount; i++)
      {     
        byte orphanedCount = _orphanedPerSector[i];
        if (orphanedCount > mostOrphaned && i != tailSector)
        {
          bestSector = i;
          mostOrphaned = orphanedCount;
        }
      }
      return bestSector;
    }

    private bool MigrateSector(ushort fromSectorId, ushort toStartClusterId = ushort.MaxValue)
    {
      ushort toClusterId = toStartClusterId;
      
      if (toStartClusterId == ushort.MaxValue) toClusterId = _tailClusterId;

      // Make sure we are not trying to migrate from/to the same sector.
      int toSectorId = toClusterId / _clustersPerSector;
      if (fromSectorId == toSectorId) return false;

      // calculate the first cluster of the specified sector.
      ushort firstClusterId = (ushort)((fromSectorId * _blockDriver.SectorSize) / _blockDriver.ClusterSize);
      
      ushort freedClusterCount = 0;

      // Loop through each cluster in the sector and move the allocated clusters to the tail leaving
      // all the orphaned clusters behind. The will leave the original sector will all inactive cluster
      // allowing us to format the sector and move the head forward. This frees the sector to be used for
      // future writes.
      for (ushort i = 0, clusterId = firstClusterId; i < _clustersPerSector; i++, clusterId++)
      {
        // Clear the defragmentation buffer
        _defragBuffer.Clear();

        // Read the first byte from the cluster, this is always the marker byte
        _blockDriver.Read(clusterId, 0, _defragBuffer, 0, 1);
        
        // Get the current state of the cluster from the marker byte
        byte marker = _defragBuffer.GetMarker();

        // Check if this is an active allocated cluster
        if (marker == BlockMarkers.AllocatedCluster)
        {
          // Read the rest of the header
          _blockDriver.Read(clusterId, 1, _defragBuffer, 1, ClusterBuffer.DataClusterHeaderSize);
          ushort blockId = _defragBuffer.GetBlockId();
          ushort dataLength = _defragBuffer.GetDataLength();

          if (blockId == 0)
          {
            _blockDriver.Read(clusterId, ClusterBuffer.DataClusterHeaderSize, _defragBuffer, ClusterBuffer.DataClusterHeaderSize,
              (ClusterBuffer.FileClusterHeaderSize - ClusterBuffer.DataClusterHeaderSize) + dataLength);
            _defragBuffer.MaxWrite = ClusterBuffer.FileClusterHeaderSize + dataLength;
          }
          else
          {
            _blockDriver.Read(clusterId, ClusterBuffer.DataClusterHeaderSize, _defragBuffer, ClusterBuffer.DataClusterHeaderSize, dataLength);
            _defragBuffer.MaxWrite = ClusterBuffer.DataClusterHeaderSize + dataLength;
          }

          // Write the defrag cluster to the tail of the log          
          WriteToCluster(toClusterId, _defragBuffer, 0, _defragBuffer.MaxWrite);          
          
          // Update any in memory file indexes with the new cluster id for the block of the file.
          var f = (FileRef)_filesIndex[_defragBuffer.GetObjId()];
          if (f != null)
          {
            f.Blocks[_defragBuffer.GetBlockId()] = toClusterId;
          }
          else
          {
            MarkClusterOrphaned(toClusterId);
            freedClusterCount++;
            _orphanedClusterCount++;
          }

          toClusterId = (ushort)((toClusterId + 1) % _totalClusterCount);
        }
        else if (marker != BlockMarkers.FormattedSector && marker != BlockMarkers.ErasedSector)
        {
          // Count the number of clusters that are not active. This will be the number of cluster
          // that this mugration of the sector has managed to free to re-use.
          freedClusterCount++;
        }
      }

      // If we where writing to the tail of the log then we need to update the tail to
      // point to the new tail after the writes have completed.
      if (toStartClusterId == ushort.MaxValue) _tailClusterId = toClusterId;

      // Once the active data has been migrated we can erase the current sector
      _blockDriver.EraseSector(fromSectorId);
      _orphanedPerSector[fromSectorId] = 0;

      // Write the marker to the sectors first byte indicating that this is a formatted sector
      _blockDriver.Write(firstClusterId, 0, BlockMarkers.FormattedSectorBytes, 0, 1);

      // Update the free cluster and orphaned cluster counters
      _freeClusterCount += freedClusterCount;
      _orphanedClusterCount -= freedClusterCount;
      Debug.Assert(_orphanedClusterCount < _totalClusterCount);

      return true;
    }

    private void MarkClusterAllocated(ushort clusterId)
    {
      // Write the allocated marker to the cluster
      _blockDriver.Write(clusterId, 0, BlockMarkers.AllocatedClusterBytes, 0, 1);
    }

    private void MarkClusterOrphaned(ushort clusterId)
    {
      // Write the orphaned marker to the cluster
      _blockDriver.Write(clusterId, 0, BlockMarkers.OrphanedClusterBytes, 0, 1);

      // Update the orphaned cluster counter.
      _orphanedClusterCount++;
      _orphanedPerSector[clusterId / _clustersPerSector]++;
    }
  }
}

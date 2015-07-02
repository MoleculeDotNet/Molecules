using System;
using Microsoft.SPOT;
using System.IO;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// Provides a stream for a file in the Tiny File System.
  /// </summary>
  class TinyFileStream : Stream
  {
    private TinyFileSystem _fs;
    private FileRef _fileRef;
    private long _filePointer;

    /// <summary>
    /// Creates an instance of TinyFileStream.
    /// </summary>
    /// <param name="fileSystem">TinyFileSystem instance on which the file this stream exposes exists.</param>
    /// <param name="fileRef">The file to expose through the TinyFileStream.</param>
    /// <remarks>
    /// Instances of this class should never be created directly. The should be created by calls to Create or Open
    /// on the TinyFileSystem instance.
    /// </remarks>
    internal TinyFileStream(TinyFileSystem fileSystem, FileRef fileRef)
    {
      _fs = fileSystem;
      _fileRef = fileRef;
      _filePointer = 0;
      _fileRef.OpenCount++;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    public override bool CanRead
    {
      get { return true; }
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    public override bool CanSeek
    {
      get { return true; }
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// </summary>
    public override bool CanWrite
    {
      get { return true; }
    }

    /// <summary>
    /// Gets length of bytes in the stream.
    /// </summary>
    public override long Length
    {
      get { CheckState(); return _fileRef.FileSize; }
    }

    /// <summary>
    /// Gets or sets the current possition in the stream.
    /// </summary>
    public override long Position
    {
      get
      {
        CheckState();
        return _filePointer;
      }
      set
      {
        Seek(value, SeekOrigin.Begin);
      }
    }

    /// <summary>
    /// Writes unwritten data to the file.
    /// </summary>
    public override void Flush()
    {
      CheckState();
    }

    /// <summary>
    /// Reads a block of bytes from the stream.
    /// </summary>
    /// <param name="array">The array to fill with the data read from the file.</param>
    /// <param name="offset">The byte offset in the array at which read bytes will be placed.</param>
    /// <param name="count">The maximun number of bytes to read.</param>
    /// <returns></returns>
    public override int Read(byte[] array, int offset, int count)
    {
      CheckState();

      if (array == null) throw new ArgumentNullException("data");
      if (offset < 0) throw new ArgumentOutOfRangeException("offset");
      if (count < 0) throw new ArgumentOutOfRangeException("count");
      if (array.Length - offset < count) throw new ArgumentOutOfRangeException("count");

      int bytesRead = _fs.Read(_fileRef, _filePointer, array, offset, count);
      _filePointer += bytesRead;
      return bytesRead;
    }

    /// <summary>
    /// Sets the current position of this stream to a given value.
    /// </summary>
    /// <param name="offset">The offset of the positon relative to the origin.</param>
    /// <param name="origin">Specified the beginning, end or current postion as a reference point to apply the offset.</param>
    /// <returns>The new postion in the stream.</returns>    
    public override long Seek(long offset, SeekOrigin origin)
    {
      CheckState();

      long newFilePointer = _filePointer;

      switch (origin)
      {
        case SeekOrigin.Begin:
          newFilePointer = offset;
          break;
        case SeekOrigin.End:
          newFilePointer = _fileRef.FileSize + offset;
          break;
        case SeekOrigin.Current:
          newFilePointer = _filePointer + offset;
          break;
      }

      if (newFilePointer < 0 || newFilePointer > _fileRef.FileSize) throw new IOException(StringTable.Error_OutOfBounds, (int)IOException.IOExceptionErrorCode.Others);

      _filePointer = newFilePointer;

      return _filePointer;
    }

    /// <summary>
    /// Sets the length of this stream to a given value.
    /// </summary>
    /// <param name="value">The new length of the stream</param>
    /// <remarks>
    /// If the length is less than the current length of the stream, the stream is truncated.
    /// </remarks>
    public override void SetLength(long value)
    {
      CheckState();
      if (value < 0 || value > _fileRef.FileSize)
      {
        throw new IOException(StringTable.Error_OutOfBounds, (int)IOException.IOExceptionErrorCode.Others);
      }
      Flush();
      _fs.Truncate(_fileRef, value);
      Seek(0, SeekOrigin.End);
    }

    /// <summary>
    /// Writes a block of bytes to the file stream.
    /// </summary>
    /// <param name="array">The buffer containing the data to write to the stream.</param>
    /// <param name="offset">The byte offset in the array from which to start writing bytes to the stream.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] array, int offset, int count)
    {
      CheckState();
      if (array == null) throw new ArgumentNullException("data");
      if (offset < 0) throw new ArgumentOutOfRangeException("offset");
      if (count < 0) throw new ArgumentOutOfRangeException("count");
      if (array.Length - offset < count) throw new ArgumentOutOfRangeException("count");

      _fs.Write(_fileRef, _filePointer, array, offset, count);
      _filePointer += count;
    }

    /// <summary>
    /// Dispose the TinyFileStream.
    /// </summary>
    /// <param name="disposing">true if being disposed from a call to Dispose otherwise false if called from the finalizer.</param>
    protected override void Dispose(bool disposing)
    {
      if (_fileRef != null)
      {
        _fileRef.OpenCount--;
        _fileRef = null;
        _fs = null;
      }

      base.Dispose(disposing);
    }

    private void CheckState()
    {
      if (_fs == null || _fileRef == null || (_fileRef != null && _fileRef.OpenCount == 0)) throw new ObjectDisposedException(StringTable.Error_FileClosed);
    }
  }
}

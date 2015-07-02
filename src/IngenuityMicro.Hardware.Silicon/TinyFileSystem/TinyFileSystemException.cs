using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Silicon
{
  [Serializable]
  public class TinyFileSystemException : Exception
  {
    private TfsErrorCode _errorCode;

    public TinyFileSystemException()
      : base()
    {
    }

    public TinyFileSystemException(string message)
      : base(message)
    {
    }

    public TinyFileSystemException(string message, TfsErrorCode errorCode)
      : base(message)
    {
      _errorCode = errorCode;
    }

    public TinyFileSystemException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    public TfsErrorCode ErrorCode { get { return _errorCode; } }

    public enum TfsErrorCode
    {
      NotFormatted = -1,
      FileInUse = -2,
      DiskFull = -3
    }
  }
}

using System;
using Microsoft.SPOT;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// Table of commonly used strings in the Tiny File System.
  /// </summary>
  static class StringTable
  {
    public const string Error_ReadPastEnd = "Read past end";
    public const string Error_WritePastEnd = "Write past end";
    public const string Error_OutOfBounds = "Out of bounds";

    public const string Error_FileNotFound = "File not found";
    public const string Error_FileAlreadyExists = "File already exists";
    public const string Error_FileIsInUse = "File is in use";
    public const string Error_FileClosed = "File closed";

    public const string Error_NotMounted = "File system has not been mounted.";    
    public const string Error_NotFormatted = "Not formatted";
    public const string Error_DiskFull = "Disk full";    
  }  
}

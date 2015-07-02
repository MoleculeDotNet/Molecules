using System;
using Microsoft.SPOT;
using System.Collections;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// General utility functions.
  /// </summary>
  class Utilities
  {
    /// <summary>
    /// Sorts an array of strings.
    /// </summary>
    /// <remarks>
    /// Original code by user "Jay Jay"
    /// http://www.tinyclr.com/codeshare/entry/475
    /// Modified to be specifically suites to sorting arrays of strings.    
    /// </remarks>
    /// <param name="array">Array of string to be sorted.</param>
    public static void Sort(string[] array)
    {
      Sort(array, 0, array.Length - 1);
    }

    /// <summary>
    /// This is a generic version of C.A.R Hoare's Quick Sort 
    /// algorithm.  This will handle arrays that are already
    /// sorted, and arrays with duplicate keys.
    /// </summary>
    /// <remarks>
    /// If you think of a one dimensional array as going from
    /// the lowest index on the left to the highest index on the right
    /// then the parameters to this function are lowest index or
    /// left and highest index or right.  The first time you call
    /// this function it will be with the parameters 0, a.length - 1.
    /// </remarks>
    /// <param name="array">Array of string to be sorted.</param>
    /// <param name="l">Left boundary of array partition</param>
    /// <param name="r">Right boundary of array partition</param>
    private static void Sort(string[] array, int l, int r)
    {
      int M = 4;
      int i;
      int j;
      string v;

      if ((r - l) <= M)
      {
        InsertionSort(array, l, r);
      }
      else
      {
        i = (r + l) / 2;
        
        if (string.Compare(array[l], array[i]) > 0)
          Swap(array, l, i);

        if (string.Compare(array[l], array[r]) > 0)
          Swap(array, l, r);

        if (string.Compare(array[i], array[r]) > 0)
          Swap(array, i, r);

        j = r - 1;
        Swap(array, i, j);

        i = l;
        v = array[j];
        for (; ; )
        {
          while (string.Compare(array[++i], v) < 0)
          { }

          while (string.Compare(array[--j], v) > 0)
          { }

          if (j < i)
            break;
          Swap(array, i, j);

        }
        Swap(array, i, r - 1);

        Sort(array, l, j);
        Sort(array, i + 1, r);
      }
    }

    private static void InsertionSort(string[] array, int lo, int hi)
    {
      int i;
      int j;
      string v;

      for (i = lo + 1; i <= hi; i++)
      {
        v = array[i];
        j = i;
        while ((j > lo) && (string.Compare(array[j - 1], v) > 0))
        {

          array[j] = array[j - 1];
          --j;
        }
        array[j] = v;
      }
    }

    private static void Swap(IList list, int left, int right)
    {
      object swap = list[left];
      list[left] = list[right];
      list[right] = swap;
    }
  }
}

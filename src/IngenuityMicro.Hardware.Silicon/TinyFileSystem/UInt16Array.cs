using System;
using Microsoft.SPOT;
using System.Collections;

namespace IngenuityMicro.Hardware.Silicon
{
  /// <summary>
  /// Dynamically growing array of UInt16 (ushort) elements.    
  /// </summary>
  class UInt16Array
  {
    private const int _defaultCapacity = 4;
    private int _capacity = _defaultCapacity;
    private int _count = 0;

    public ushort[] _array = new ushort[_defaultCapacity];

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element a the specified index.</returns>
    public ushort this[int index]
    {
      get 
      {
        if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException("index");
        return _array[index];
      }
      set { Set(index, value); }
    }

    /// <summary>
    /// Gets the number of elements contained in the array.
    /// </summary>
    public int Count { get { return _count; } }

    /// <summary>
    /// Adds an element to the end of the collection.
    /// </summary>
    /// <param name="value">Value of the element to add.</param>
    /// <returns>The new count of items in the array.</returns>
    public int Add(ushort value)
    {
      if (_count == _capacity)
      {
        Grow(_capacity << 1);
      }
      _array[_count++] = value;
      return _count;
    }

    /// <summary>
    /// Adjusts the length of the array. 
    /// This can be used to trim the end of the array.
    /// </summary>
    /// <param name="length">New length of the array.</param>
    public void SetLength(int length)
    {
      if (length < 0) throw new ArgumentOutOfRangeException("length");
      if (length > _count)
      {
        if (length > _capacity) Grow(_defaultCapacity + (int)System.Math.Ceiling((double)length / _defaultCapacity) * _defaultCapacity);
      }
      _count = length;
    }

    /// <summary>
    /// Sets the value of an element at the specified index.
    /// If the index is beyond the end of the array, the array will grow 
    /// to accomodate the new element.
    /// </summary>
    /// <param name="index">The zero-based index of the element to set.</param>
    /// <param name="value"></param>
    private void Set(int index, ushort value)
    {
      if (index < 0) throw new ArgumentOutOfRangeException("index");
      if (index >= _capacity) Grow(_defaultCapacity + (int)System.Math.Ceiling((double)index / _defaultCapacity) * _defaultCapacity);
      _array[index] = value;
      if (index >= _count) _count = index + 1;
    }

    /// <summary>
    /// Grows the internal array to increase the capacity.
    /// </summary>
    /// <param name="newSize">New size of the array.</param>
    private void Grow(int newSize)
    {
      ushort[] newArray = new ushort[newSize];
      Array.Copy(_array, newArray, _capacity);
      _capacity = newArray.Length;
      _array = newArray;
    }
  } 
}

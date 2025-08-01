using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Generators;

/// <summary>
/// An immutable, equatable array wrapper for use in incremental generators.
/// Provides value-based equality for proper caching.
/// Source: https://github.com/andrewlock/blog-examples/blob/master/NetEscapades.EnumGenerators/src/NetEscapades.EnumGenerators/EquatableArray.cs
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new([]);

    private readonly T[]? _array;

    public EquatableArray(T[]? array)
    {
        _array = array;
    }

    public EquatableArray(IEnumerable<T> items)
    {
        _array = items?.ToArray();
    }

    public T[] ToArray() => _array?.ToArray() ?? [];

    public int Count => _array?.Length ?? 0;

    public T this[int index] => _array![index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array == other._array)
            return true;

        if (_array is null || other._array is null)
            return false;

        if (_array.Length != other._array.Length)
            return false;

        for (int i = 0; i < _array.Length; i++)
        {
            if (!_array[i].Equals(other._array[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (_array is null)
            return 0;

        unchecked
        {
            int hash = 17;
            foreach (var item in _array)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return (_array ?? []).AsEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}

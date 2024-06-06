using System;
using Titanium.Web.Proxy.Extensions;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.Http;

/// <summary>
/// Represents a known HTTP header.
/// </summary>
public class KnownHeader
{
    /// <summary>
    /// The string representation of the header.
    /// </summary>
    public string String;

    /// <summary>
    /// The ByteString representation of the header.
    /// </summary>
    internal ByteString String8;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnownHeader"/> class.
    /// </summary>
    /// <param name="str">The string representation of the header.</param>
    private KnownHeader(string str)
    {
        String8 = (ByteString)str;
        String = str;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return String;
    }

    /// <summary>
    /// Determines whether the specified value is equal to the current object.
    /// </summary>
    /// <param name="value">The value to compare with the current object.</param>
    /// <returns>true if the specified value is equal to the current object; otherwise, false.</returns>
    internal bool Equals(ReadOnlySpan<char> value)
    {
        return String.AsSpan().EqualsIgnoreCase(value);
    }

    /// <summary>
    /// Determines whether the specified value is equal to the current object.
    /// </summary>
    /// <param name="value">The value to compare with the current object.</param>
    /// <returns>true if the specified value is equal to the current object; otherwise, false.</returns>
    internal bool Equals(string? value)
    {
        return String.EqualsIgnoreCase(value);
    }

    /// <summary>
    /// Converts a string to a <see cref="KnownHeader"/>.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    public static implicit operator KnownHeader(string str)
    {
        return new(str);
    }
}

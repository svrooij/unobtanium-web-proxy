using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Unobtanium.Web.Proxy.Extensions;

namespace Unobtanium.Web.Proxy.UnitTests.Extensions;

[TestClass]
public class StringExtensionsTests
{
    [TestMethod]
    public void ByteArrayToHexString_GivenByteArray_ReturnsCorrectHexString ()
    {
        // Arrange
        ReadOnlySpan<byte> data = [0x1A, 0x2B, 0x3C];
        string expectedHexString = "1A 2B 3C";

        // Act
        string hexString = data.ByteArrayToHexString();

        // Assert
        Assert.AreEqual(expectedHexString, hexString);
    }

    [TestMethod]
    public void ByteArrayToHexString_GivenEmptyArray_ReturnsEmptyString ()
    {
        // Arrange
        ReadOnlySpan<byte> data = [];
        string expectedHexString = "";

        // Act
        string hexString = data.ByteArrayToHexString();

        // Assert
        Assert.AreEqual(expectedHexString, hexString);
    }

    [TestMethod]
    public void EqualsIgnoreCase_WithEqualStrings_ReturnsTrue ()
    {
        string str = "HelloWorld";
        string value = "helloworld";

        bool result = str.EqualsIgnoreCase(value);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void EqualsIgnoreCase_WithDifferentStrings_ReturnsFalse ()
    {
        string str = "HelloWorld";
        string value = "GoodbyeWorld";

        bool result = str.EqualsIgnoreCase(value);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void EqualsIgnoreCase_WithEqualSpans_ReturnsTrue ()
    {
        var str = "HelloWorld".AsSpan();
        var value = "helloworld".AsSpan();

        bool result = str.EqualsIgnoreCase(value);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void EqualsIgnoreCase_WithDifferentSpans_ReturnsFalse ()
    {
        var str = "HelloWorld".AsSpan();
        var value = "GoodbyeWorld".AsSpan();

        bool result = str.EqualsIgnoreCase(value);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsIgnoreCase_WithStringContainingValue_ReturnsTrue ()
    {
        string str = "HelloWorld";
        string value = "WORLD";

        bool result = str.ContainsIgnoreCase(value);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsIgnoreCase_WithStringNotContainingValue_ReturnsFalse ()
    {
        string str = "HelloWorld";
        string value = "Universe";

        bool result = str.ContainsIgnoreCase(value);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsIgnoreCase_WithStringNullValue_ReturnsFalse ()
    {
        string str = "HelloWorld";

        bool result = str.ContainsIgnoreCase(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IndexOfIgnoreCase_WithStringContainingValue_ReturnsCorrectIndex ()
    {
        string str = "HelloWorld";
        string value = "WORLD";

        int result = str.IndexOfIgnoreCase(value);

        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void IndexOfIgnoreCase_WithStringNotContainingValue_ReturnsMinusOne ()
    {
        string str = "HelloWorld";
        string value = "Universe";

        int result = str.IndexOfIgnoreCase(value);

        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void IndexOfIgnoreCase_WithStringNullValue_ReturnsMinusOne ()
    {
        string str = "HelloWorld";

        int result = str.IndexOfIgnoreCase(null);

        Assert.AreEqual(-1, result);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Extensions;

namespace Titanium.Web.Proxy.UnitTests.Extensions;
[TestClass]
public class HttpHeaderExtensionsTests
{
    [TestMethod]
    public void GetString_GivenByteString_ReturnsCorrectString ()
    {
        // Arrange
        var data = new ByteString(new byte[] { 0x1A, 0x2B, 0x3C });
        string expectedString = "\u001a+<";

        // Act
        string str = data.GetString();

        // Assert
        Assert.AreEqual(expectedString, str);
    }

    [TestMethod]
    public void GetString_GivenEmptyByteString_ReturnsEmptyString ()
    {
        // Arrange
        var data = new ByteString(new byte[] { });
        string expectedString = "";

        // Act
        string str = data.GetString();

        // Assert
        Assert.AreEqual(expectedString, str);
    }

    [TestMethod]
    public void GetByteString_GivenString_ReturnsCorrectByteString ()
    {
        // Arrange
        string str = "HelloWorld";
        ByteString expectedByteString = new ByteString(Encoding.UTF8.GetBytes(str));

        // Act
        ByteString byteString = str.GetByteString();

        // Assert
        Assert.AreEqual(expectedByteString, byteString);
    }
}

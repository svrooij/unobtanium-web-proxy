using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Titanium.Web.Proxy.Compression;

namespace Titanium.Web.Proxy.UnitTests;

[TestClass]
public class CompressionFactoryTests
{
    [TestMethod]
    [DataRow(HttpCompression.Gzip, typeof(GZipStream))]
    [DataRow(HttpCompression.Deflate, typeof(DeflateStream))]
    [DataRow(HttpCompression.Brotli, typeof(BrotliSharpLib.BrotliStream))]
    public void Create_ShouldReturnCorrectStream ( HttpCompression type, System.Type expectedType )
    {
        using var memoryStream = new MemoryStream();
        var result = CompressionFactory.Create(type, memoryStream);

        Assert.IsInstanceOfType(result, expectedType);
    }

    [TestMethod]
    [ExpectedException(typeof(System.InvalidOperationException))]
    public void Create_ShouldThrowExceptionForUnsupportedCompression ()
    {
        using var memoryStream = new MemoryStream();
        CompressionFactory.Create((HttpCompression)999, memoryStream);
    }
}

[TestClass]
public class CompressionUtilTests
{
    [TestMethod]
    public void CompressionNameToEnum_ShouldReturnCorrectEnum ()
    {
        // Test for Gzip
        Assert.AreEqual(HttpCompression.Gzip, CompressionUtil.CompressionNameToEnum("gzip"));

        // Test for Deflate
        Assert.AreEqual(HttpCompression.Deflate, CompressionUtil.CompressionNameToEnum("deflate"));

        // Test for Brotli
        Assert.AreEqual(HttpCompression.Brotli, CompressionUtil.CompressionNameToEnum("br"));

        // Test for Unsupported
        Assert.AreEqual(HttpCompression.Unsupported, CompressionUtil.CompressionNameToEnum("something"));
    }
}

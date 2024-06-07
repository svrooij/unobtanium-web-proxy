using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Titanium.Web.Proxy.Compression;

namespace Titanium.Web.Proxy.UnitTests
{
    [TestClass]
    public class DecompressionFactoryTests
    {
        [TestMethod]
        [DataRow(HttpCompression.Gzip, typeof(GZipStream))]
        [DataRow(HttpCompression.Deflate, typeof(DeflateStream))]
        [DataRow(HttpCompression.Brotli, typeof(BrotliSharpLib.BrotliStream))]
        public void Create_ShouldReturnCorrectStream(HttpCompression type, System.Type expectedType)
        {
            using var memoryStream = new MemoryStream();
            var result = DecompressionFactory.Create(type, memoryStream);

            Assert.IsInstanceOfType(result, expectedType);
        }

        [TestMethod]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void Create_ShouldThrowExceptionForUnsupportedCompression()
        {
            using var memoryStream = new MemoryStream();
            DecompressionFactory.Create((HttpCompression)999, memoryStream);
        }
    }
}
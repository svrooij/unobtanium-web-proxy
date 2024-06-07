using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.UnitTests;

[TestClass]
public class NullWriterTests
{
    private NullWriter nullWriter;

    [TestInitialize]
    public void TestInitialize()
    {
        nullWriter = NullWriter.Instance;
    }

    [TestMethod]
    public void IsNetworkStream_ShouldReturnFalse()
    {
        Assert.IsFalse(nullWriter.IsNetworkStream);
    }

    [TestMethod]
    public void Write_ShouldNotThrowException()
    {
        var buffer = new byte[10];
        nullWriter.Write(buffer, 0, buffer.Length);
    }

    [TestMethod]
    public async Task WriteAsync_ShouldNotThrowException()
    {
        var buffer = new byte[10];
        await nullWriter.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);
    }

    [TestMethod]
    public async Task WriteLineAsync_ShouldThrowNotImplementedException()
    {
        await Assert.ThrowsExceptionAsync<NotImplementedException>(async () => await nullWriter.WriteLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteLineAsyncWithValue_ShouldThrowNotImplementedException()
    {
        await Assert.ThrowsExceptionAsync<NotImplementedException>(async () => await nullWriter.WriteLineAsync("test", CancellationToken.None));
    }
}

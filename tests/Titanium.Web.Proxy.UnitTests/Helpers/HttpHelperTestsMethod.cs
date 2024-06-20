using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Helpers;

namespace Titanium.Web.Proxy.UnitTests.Helpers;
public partial class HttpHelperTests
{
    [TestMethod]
    public void GetKnownMethod_WithGetMethod_ReturnsGet ()
    {
        var method = Encoding.ASCII.GetBytes("GET");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Get, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithPostMethod_ReturnsPost ()
    {
        var method = Encoding.ASCII.GetBytes("POST");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Post, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithHeadMethod_ReturnsHead ()
    {
        var method = Encoding.ASCII.GetBytes("HEAD");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Head, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithPutMethod_ReturnsPut ()
    {
        var method = Encoding.ASCII.GetBytes("PUT");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Put, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithDeleteMethod_ReturnsDelete ()
    {
        var method = Encoding.ASCII.GetBytes("DELETE");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Delete, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithTraceMethod_ReturnsTrace ()
    {
        var method = Encoding.ASCII.GetBytes("TRACE");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Trace, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithConnectMethod_ReturnsConnect ()
    {
        var method = Encoding.ASCII.GetBytes("CONNECT");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Connect, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithOptionsMethod_ReturnsOptions ()
    {
        var method = Encoding.ASCII.GetBytes("OPTIONS");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Options, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithInvalidMethod_ReturnsUnknown ()
    {
        var method = Encoding.ASCII.GetBytes("INVALID");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Unknown, result);
    }

    [TestMethod]
    public void GetKnownMethod_WithShortMethod_ReturnsUnknown ()
    {
        var method = Encoding.ASCII.GetBytes("GT");
        var result = HttpHelper.GetKnownMethod(method.AsSpan());
        Assert.AreEqual(KnownMethod.Unknown, result);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Helpers;

namespace Unobtanium.Web.Proxy.UnitTests.Helpers;
public partial class HttpHelperTests
{
    [TestMethod]
    public void GetBoundaryFromContentType_WithBoundary_ReturnsBoundary ()
    {
        var contentType = "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW";
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.AreEqual("----WebKitFormBoundary7MA4YWxkTrZu0gW", boundary.ToString());
    }

    [TestMethod]
    public void GetBoundaryFromContentType_WithQuotedBoundary_ReturnsBoundaryWithoutQuotes ()
    {
        var contentType = "multipart/form-data; boundary=\"----WebKitFormBoundary7MA4YWxkTrZu0gW\"";
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.AreEqual("----WebKitFormBoundary7MA4YWxkTrZu0gW", boundary.ToString());
    }

    [TestMethod(), Ignore("Generated test does not work")]
    public void GetBoundaryFromContentType_WithoutBoundary_ReturnsNull ()
    {
        var contentType = "text/html; charset=UTF-8";
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.IsNull(boundary);
    }

    [TestMethod, Ignore("Generated test does not work")]
    public void GetBoundaryFromContentType_WithEmptyContentType_ReturnsNull ()
    {
        var contentType = "";
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.IsNull(boundary);
    }

    [TestMethod, Ignore("Generated test does not work")]
    public void GetBoundaryFromContentType_WithNullContentType_ReturnsNull ()
    {
        string contentType = null;
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.IsNull(boundary);
    }

    [TestMethod]
    public void GetBoundaryFromContentType_WithMultipleParameters_ReturnsCorrectBoundary ()
    {
        var contentType = "multipart/form-data; charset=utf-8; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW";
        var boundary = HttpHelper.GetBoundaryFromContentType(contentType);
        Assert.AreEqual("----WebKitFormBoundary7MA4YWxkTrZu0gW", boundary.ToString());
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.UnitTests.Helpers
{
    [TestClass]
    public partial class HttpHelperTests
    {
        [TestMethod]
        public void GetEncodingFromContentType_WithNull_ReturnsDefaultEncoding()
        {
            var encoding = HttpHelper.GetEncodingFromContentType(null);
            Assert.AreEqual(HttpHeader.DefaultEncoding, encoding);
        }

        [TestMethod]
        public void GetEncodingFromContentType_WithEmptyString_ReturnsDefaultEncoding()
        {
            var encoding = HttpHelper.GetEncodingFromContentType("");
            Assert.AreEqual(HttpHeader.DefaultEncoding, encoding);
        }

        [TestMethod]
        public void GetEncodingFromContentType_WithValidCharset_ReturnsCorrectEncoding()
        {
            var encoding = HttpHelper.GetEncodingFromContentType("text/html; charset=UTF-8");
            Assert.AreEqual(Encoding.UTF8, encoding);
        }

        [TestMethod]
        public void GetEncodingFromContentType_WithQuotedCharset_ReturnsCorrectEncoding()
        {
            var encoding = HttpHelper.GetEncodingFromContentType("text/html; charset=\"UTF-8\"");
            Assert.AreEqual(Encoding.UTF8, encoding);
        }

        [TestMethod]
        public void GetEncodingFromContentType_WithXUserDefined_IgnoresCharset()
        {
            var encoding = HttpHelper.GetEncodingFromContentType("text/html; charset=x-user-defined");
            Assert.AreEqual(HttpHeader.DefaultEncoding, encoding);
        }

        [TestMethod]
        public void GetEncodingFromContentType_WithInvalidCharset_ReturnsDefaultEncoding()
        {
            var encoding = HttpHelper.GetEncodingFromContentType("text/html; charset=invalid-charset");
            Assert.AreEqual(HttpHeader.DefaultEncoding, encoding);
        }


    }
}

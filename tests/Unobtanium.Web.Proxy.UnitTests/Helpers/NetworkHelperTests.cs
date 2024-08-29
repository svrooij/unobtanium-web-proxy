using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using Unobtanium.Web.Proxy.Helpers;

namespace Unobtanium.Web.Proxy.UnitTests.Helpers
{
    [TestClass]
    public class NetworkHelperTests
    {
        [TestMethod]
        public void IsLocalIpAddress_WithLoopbackAddress_ReturnsTrue()
        {
            var loopbackAddress = IPAddress.Loopback;
            var result = NetworkHelper.IsLocalIpAddress(loopbackAddress);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsLocalIpAddress_WithLocalHostName_ReturnsTrue()
        {
            var localHostName = Dns.GetHostName();
            var result = NetworkHelper.IsLocalIpAddress(localHostName);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsLocalIpAddress_WithLocalhost_ReturnsTrue()
        {
            var localhost = "localhost";
            var result = NetworkHelper.IsLocalIpAddress(localhost);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsLocalIpAddress_WithInvalidHostName_ReturnsFalse()
        {
            var invalidHostName = "invalid.hostname";
            var result = NetworkHelper.IsLocalIpAddress(invalidHostName);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLocalIpAddress_WithExternalIpAddress_ReturnsFalse()
        {
            // Use a well-known external IP address (Google DNS)
            var externalIpAddress = IPAddress.Parse("8.8.8.8");
            var result = NetworkHelper.IsLocalIpAddress(externalIpAddress);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLocalIpAddress_WithExternalHostName_ReturnsFalse()
        {
            // Use a well-known external hostname (Google)
            var externalHostName = "google.com";
            var result = NetworkHelper.IsLocalIpAddress(externalHostName);
            Assert.IsFalse(result);
        }

        // Additional tests can be added for proxyDnsRequests scenarios
    }
}

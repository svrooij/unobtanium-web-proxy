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
    public void GetWildCardDomainName_WithSubdomain_ReturnsWildcard ()
    {
        var hostname = "www.example.com";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("*.example.com", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithoutSubdomain_ReturnsSameHostname ()
    {
        var hostname = "example.com";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("example.com", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithIPAddress_ReturnsSameIPAddress ()
    {
        var hostname = "192.168.1.1";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("192.168.1.1", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithDisableWildCardCertificates_ReturnsSameHostname ()
    {
        var hostname = "www.example.com";
        var result = HttpHelper.GetWildCardDomainName(hostname, true);
        Assert.AreEqual("www.example.com", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithSecondLevelDomain_ReturnsSameHostname ()
    {
        var hostname = "example.co.uk";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("example.co.uk", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithSubdomainContainingDash_ReturnsSameHostname ()
    {
        var hostname = "sub-domain.example.com";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("sub-domain.example.com", result);
    }

    [TestMethod]
    public void GetWildCardDomainName_WithSecondLevelDomainShortLength_ReturnsSameHostname ()
    {
        var hostname = "example.vn.ua";
        var result = HttpHelper.GetWildCardDomainName(hostname, false);
        Assert.AreEqual("example.vn.ua", result);
    }
}

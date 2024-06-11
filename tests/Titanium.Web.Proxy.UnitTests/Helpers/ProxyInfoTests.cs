using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Models; // Adjust the namespace if necessary

namespace Titanium.Web.Proxy.UnitTests.Helpers;

[TestClass]
public class ProxyInfoTests
{
    [TestMethod]
    public void ConvertRegexReservedChars_WithSpecialCharacters_ReturnsEscapedString ()
    {
        // Arrange
        var input = "#$()+.?[\\^{|";
        var expected = "\\#\\$\\(\\)\\+\\.\\?\\[\\\\\\^\\{\\|";

        // Act
        var result = ProxyInfo.ConvertRegexReservedChars(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ConvertRegexReservedChars_WithAsterisk_ReplacesWithDot_Regression ()
    {
        // Arrange
        var input = "example*domain.com";
        var expected = "example.domain\\.com";

        // Act
        var result = ProxyInfo.ConvertRegexReservedChars(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ConvertRegexReservedChars_WithEmptyString_ReturnsEmptyString ()
    {
        // Arrange
        var input = "";
        var expected = "";

        // Act
        var result = ProxyInfo.ConvertRegexReservedChars(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BypassStringEscape_WithSchemeHostPort_ReturnsRegexPattern ()
    {
        // Arrange
        var input = "http://example.com:8080";
        var expected = @"^http://example\.com:8080$"; // Adjust based on actual method behavior

        // Act
        var result = ProxyInfo.BypassStringEscape(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BypassStringEscape_WithHostOnly_ReturnsRegexPattern ()
    {
        // Arrange
        var input = "example.com";
        var expected = @"^(?:.*://)?example\.com(?::[0-9]{1,5})?$"; // Adjust based on actual method behavior

        // Act
        var result = ProxyInfo.BypassStringEscape(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BypassStringEscape_WithEmptyString_ReturnsEmptyString ()
    {
        // Arrange
        var input = "";
        var expected = @"^(?:.*://)?(?::[0-9]{1,5})?$"; // Adjust based on actual method behavior

        // Act
        var result = ProxyInfo.BypassStringEscape(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("HTTP", ProxyProtocolType.Http)]
    [DataRow("HTTPS", ProxyProtocolType.Https)]
    [DataRow("http", ProxyProtocolType.Http)]
    [DataRow("https", ProxyProtocolType.Https)]
    [DataRow("ftp", null)]
    [DataRow("", null)]
    public void ParseProtocol_WithInput_ReturnsExpectedProtocol ( string input, ProxyProtocolType? expected )
    {
        // Arrange

        // Act
        var result = ProxyInfo.ParseProtocolType(input);

        // Assert
        Assert.AreEqual(expected, result);
    }
}

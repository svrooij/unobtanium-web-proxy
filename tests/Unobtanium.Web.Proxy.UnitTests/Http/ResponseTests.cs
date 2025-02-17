using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Exceptions;

namespace Unobtanium.Web.Proxy.UnitTests;
[TestClass]
public class ResponseTests
{
    [TestMethod]
    [ExpectedException(typeof(BodyNotFoundException))]
    public void EnsureBodyAvailable_ThrowsException_WhenMethodIsHead ()
    {
        // Arrange
        var response = new Response();
        response.RequestMethod = "HEAD"; // Assuming GET requests don't have a body

        // Act
        response.EnsureBodyAvailable();
    }



    [TestMethod]
    [ExpectedException(typeof(BodyNotLoadedException))]
    public void EnsureBodyAvailable_ThrowsException_WhenNotLoaded ()
    {
        // Arrange
        var response = new Response();
        response.RequestMethod = "POST";
        response.ContentLength = 10;
        response.IsBodyRead = false;

        // Act
        response.EnsureBodyAvailable();
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Exceptions;

namespace Unobtanium.Web.Proxy.UnitTests;
[TestClass]
public class RequestTests
{
    [TestMethod]
    [ExpectedException(typeof(BodyNotFoundException))]
    public void EnsureBodyAvailable_ThrowsException_WhenMethodIsGET ()
    {
        // Arrange
        var request = new Request();
        request.Method = "GET"; // Assuming GET requests don't have a body

        // Act
        request.EnsureBodyAvailable();
    }

    [TestMethod]
    [ExpectedException(typeof(BodyNotFoundException))]
    public void EnsureBodyAvailable_ThrowsException_WhenContentLengthIs0 ()
    {
        // Arrange
        var request = new Request();
        request.Method = "POST"; // Assuming GET requests don't have a body
        request.ContentLength = 0;

        // Act
        request.EnsureBodyAvailable();
    }

    [TestMethod]
    [ExpectedException(typeof(BodyLockedException))]
    public void EnsureBodyAvailable_ThrowsException_WhenIsLocked ()
    {
        // Arrange
        var request = new Request();
        request.Method = "POST";
        request.ContentLength = 10;
        request.Locked = true;

        // Act
        request.EnsureBodyAvailable();
    }

    [TestMethod]
    [ExpectedException(typeof(BodyNotLoadedException))]
    public void EnsureBodyAvailable_ThrowsException_WhenNotLoaded ()
    {
        // Arrange
        var request = new Request();
        request.Method = "POST";
        request.ContentLength = 10;

        // Act
        request.EnsureBodyAvailable();
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.UnitTests
{
    [TestClass]
    public class ProxyServerTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Expected argument exception for re-using the port")]
        public void GivenOneEndpointIsAlreadyAddedToAddress_WhenAddingNewEndpointToExistingAddress_ThenExceptionIsThrown ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 9999;
            var firstIpAddress = IPAddress.Parse("127.0.0.1");
            var secondIpAddress = IPAddress.Parse("127.0.0.1");
            proxy.AddEndPoint(new ExplicitProxyEndPoint(firstIpAddress, port, false));

            // This should throw an exception
            proxy.AddEndPoint(new ExplicitProxyEndPoint(secondIpAddress, port, false));
        }

        [TestMethod]
        public void GivenOneEndpointIsAlreadyAddedToAddress_WhenAddingNewEndpointToExistingAddress_ThenTwoEndpointsExists ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 9999;
            var firstIpAddress = IPAddress.Parse("127.0.0.1");
            var secondIpAddress = IPAddress.Parse("192.168.1.1");
            proxy.AddEndPoint(new ExplicitProxyEndPoint(firstIpAddress, port, false));

            // Act
            proxy.AddEndPoint(new ExplicitProxyEndPoint(secondIpAddress, port, false));

            // Assert
            Assert.AreEqual(2, proxy.ProxyEndPoints.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Expected argument exception for re-using the port")]
        public void GivenOneEndpointIsAlreadyAddedToPort_WhenAddingNewEndpointToExistingPort_ThenExceptionIsThrown ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 9999;
            proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, port, false));

            // This should throw an exception
            proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, port, false));
        }

        [TestMethod]
        public void GivenOneEndpointIsAlreadyAddedToZeroPort_WhenAddingNewEndpointToExistingPort_ThenTwoEndpointsExists ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 0;
            proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, port, false));

            // Act
            proxy.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, port, false));

            // Assert
            Assert.AreEqual(2, proxy.ProxyEndPoints.Count);
        }

        [TestMethod]
        public void GivenOneEndpointIsAdded_WhenRemovingEndpoint_Succeeds ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 0;
            var endpoint = new ExplicitProxyEndPoint(IPAddress.Loopback, port, false);
            proxy.AddEndPoint(endpoint);

            // Act
            proxy.RemoveEndPoint(endpoint);

            // Assert
            Assert.AreEqual(0, proxy.ProxyEndPoints.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "Expected argument exception for removing not existing endpoint")]
        public void GivenNoEndpointIsAdded_WhenRemovingEndpoint_ThrowsArgumentException ()
        {
            // Arrange
            var proxy = new ProxyServer();
            const int port = 0;
            var endpoint = new ExplicitProxyEndPoint(IPAddress.Loopback, port, false);

            // This should throw
            proxy.RemoveEndPoint(endpoint);
        }

        [TestMethod]
        public async Task InvokeClientConnectionCreateEvent_WhenCalled_InvokesEventHandler ()
        {
            var proxy = new ProxyServer();
            bool isHit = false;
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            proxy.OnClientConnectionCreate += ( sender, args ) =>
            {
                isHit = true;
                Assert.AreEqual(socket, args);
                return Task.CompletedTask;
            };


            // Act
            await proxy.InvokeClientConnectionCreateEvent(socket);

            // Assert
            Assert.IsTrue(isHit);

        }

        [TestMethod]
        public async Task InvokeServerConnectionCreateEvent_WhenCalled_InvokesEventHandler ()
        {
            var proxy = new ProxyServer();
            bool isHit = false;
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            proxy.OnServerConnectionCreate += ( sender, args ) =>
            {
                isHit = true;
                Assert.AreEqual(socket, args);
                return Task.CompletedTask;
            };


            // Act
            await proxy.InvokeServerConnectionCreateEvent(socket);

            // Assert
            Assert.IsTrue(isHit);

        }

        [TestMethod]
        public void SetAsSystemProxy_ThrowsOnNonWindows ()
        {
            var proxyServer = new ProxyServer();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.ThrowsException<NotSupportedException>(() => proxyServer.SetAsSystemHttpProxy(new ExplicitProxyEndPoint(IPAddress.Loopback, 8080)));
            }
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void ThrowNotSupportedException_ThrowsException ()
        {
            var proxyServer = new ProxyServer();
            var methodInfo = typeof(ProxyServer).GetMethod("ThrowNotSupportedException", BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo == null)
            {
                Assert.Fail("Method ThrowNotSupportedException was not found.");
            }

            try
            {
                methodInfo.Invoke(proxyServer, [nameof(ThrowNotSupportedException_ThrowsException)]);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        [TestMethod]
        public void UpdateClientConnectionCount_WhenCalled_ThenEventIsRaised ()
        {
            var proxy = new ProxyServer();
            bool isHit = false;
            proxy.ClientConnectionCountChanged += ( sender, args ) =>
            {
                Assert.AreEqual(0, args.OldValue);
                Assert.AreEqual(1, args.NewValue);
                isHit = true;
            };

            proxy.UpdateClientConnectionCount(true);
            Assert.IsTrue(isHit);
        }

        [TestMethod]
        public void UpdateClientConnectionCount_WhenCalledWithThrowInHandler_ThenExceptionFunctionIsCalled ()
        {
            var proxy = new ProxyServer();
            bool isEventHit = false;
            bool isExceptionHit = false;
            var exception = new Exception("Test exception");
            proxy.ClientConnectionCountChanged += ( sender, args ) =>
            {

                isEventHit = true;
                throw exception;
            };
            proxy.ExceptionFunc = ( ex ) =>
            {
                Assert.AreEqual(exception, ex);
                isExceptionHit = true;
            };

            proxy.UpdateClientConnectionCount(true);
            Assert.IsTrue(isEventHit);
            Assert.IsTrue(isExceptionHit);
        }

        [TestMethod]
        public void UpdateClientConnectionCount_WhenCalled_ThenEventHoldsCorrectValue ()
        {
            var proxy = new ProxyServer();
            int hitCount = 0;
            proxy.ClientConnectionCountChanged += ( sender, args ) =>
            {
                Assert.AreNotEqual(args.OldValue, args.NewValue);
                hitCount++;
            };

            proxy.UpdateClientConnectionCount(true);
            proxy.UpdateClientConnectionCount(true);
            proxy.UpdateClientConnectionCount(false);
            Assert.AreEqual(hitCount, 3);
            Assert.AreEqual(1, proxy.ClientConnectionCount);
        }

        [TestMethod]
        public void UpdateServerConnectionCount_WhenCalled_ThenEventIsRaised ()
        {
            var proxy = new ProxyServer();
            bool isHit = false;
            proxy.ServerConnectionCountChanged += ( sender, args ) =>
            {
                Assert.AreEqual(0, args.OldValue);
                Assert.AreEqual(1, args.NewValue);
                isHit = true;
            };

            proxy.UpdateServerConnectionCount(true);
            Assert.IsTrue(isHit);
        }

        [TestMethod]
        public void UpdateServerConnectionCount_WhenCalled_ThenEventHoldsCorrectValue ()
        {
            var proxy = new ProxyServer();
            int hitCount = 0;
            proxy.ServerConnectionCountChanged += ( sender, args ) =>
            {
                Assert.AreNotEqual(args.OldValue, args.NewValue);
                hitCount++;
            };

            proxy.UpdateServerConnectionCount(true);
            proxy.UpdateServerConnectionCount(true);
            proxy.UpdateServerConnectionCount(false);
            Assert.AreEqual(hitCount, 3);
            Assert.AreEqual(1, proxy.ServerConnectionCount);
        }
    }
}

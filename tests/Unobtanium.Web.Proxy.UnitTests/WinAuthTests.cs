using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;
using Unobtanium.Web.Proxy.Http;
using Unobtanium.Web.Proxy.Network.WinAuth;

namespace Unobtanium.Web.Proxy.UnitTests
{
    [TestClass]
    public class WinAuthTests
    {
        [TestMethod]
        public void Test_Acquire_Client_Token ()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var token = WinAuthHandler.GetInitialAuthToken("mylocalserver.com", "NTLM", new InternalDataStore());
                Assert.IsTrue(token.Length > 1);
            }
            else
            {
                Assert.Inconclusive("This test can only be run on Windows.");
            }
        }
    }
}

using Unobtanium.Web.Proxy.EventArguments;

namespace Unobtanium.Web.Proxy.Examples.Basic
{
    public static class ProxyEventArgsBaseExtensions
    {
        public static SampleClientState GetState ( this ProxyEventArgsBase args )
        {
            if (args.ClientUserData == null) args.ClientUserData = new SampleClientState();

            return (SampleClientState)args.ClientUserData;
        }
    }
}

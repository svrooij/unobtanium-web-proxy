using System;
using System.Threading;
using System.Threading.Tasks;
using Unobtanium.Web.Proxy.Examples.Basic.Helpers;
using Unobtanium.Web.Proxy.Helpers;

namespace Unobtanium.Web.Proxy.Examples.Basic
{
    public class Program
    {
        private static readonly ProxyTestController controller = new ProxyTestController();
        private static readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        public static async Task<int> Main ( string[] args )
        {
            Console.WriteLine("========================================");
            Console.WriteLine("=========  Unobtanium Web Proxy ========");
            Console.WriteLine("========================================");

            //if (RunTime.IsWindows)
            //    // fix console hang due to QuickEdit mode
            //    ConsoleHelper.DisableQuickEditMode();

            Console.CancelKeyPress += Console_CancelKeyPress;

            // Start proxy controller
            await controller.StartProxy(tokenSource.Token);
            Console.WriteLine("CTRL + C to exit");

            while (!tokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, tokenSource.Token);
                }
                catch (TaskCanceledException) { } // Ignore all task cancelled exceptions

            }

            controller.Dispose();
            return 0;
        }

        private static void Console_CancelKeyPress ( object sender, ConsoleCancelEventArgs e )
        {
            Console.WriteLine("Received CTRL + C, stopping");
            tokenSource.Cancel();
            controller.Stop();

            Console.WriteLine("Proxy stopped greasefully");

            Environment.Exit(0);

        }
    }
}

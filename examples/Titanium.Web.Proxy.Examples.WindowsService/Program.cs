using System.Runtime.InteropServices;
using System;
using System.ServiceProcess;

namespace WindowsServiceExample
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        private static void Main ()
        {

            ServiceBase[] servicesToRun;
            servicesToRun = new ServiceBase[]
            {
                new ProxyService()
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ServiceBase.Run(servicesToRun);
            }
            else
            {
                Console.WriteLine("This application is only supported on Windows.");
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace Titanium.Web.Proxy.Helpers;

internal partial class NativeMethods
{
    // Keeps it from getting garbage collected
    internal static ConsoleEventDelegate? Handler;

    [LibraryImport("wininet.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InternetSetOption ( IntPtr hInternet, int dwOption, IntPtr lpBuffer,
        int dwBufferLength );

    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetConsoleWindow ();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetConsoleCtrlHandler ( ConsoleEventDelegate callback, [MarshalAs(UnmanagedType.Bool)] bool add );

    /// <summary>
    ///     <see href="https://docs.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-getsystemmetrics" />
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics ( int nIndex );

    // Pinvoke
    internal delegate bool ConsoleEventDelegate ( int eventType );
}

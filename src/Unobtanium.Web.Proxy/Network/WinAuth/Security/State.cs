﻿using System;

namespace Unobtanium.Web.Proxy.Network.WinAuth.Security;

/// <summary>
///     Status of authenticated session
/// </summary>
internal class State
{
    /// <summary>
    ///     States during Windows Authentication
    /// </summary>
    public enum WinAuthState
    {
        Unauthorized,
        InitialToken,
        FinalToken,
        Authorized
    }

    /// <summary>
    ///     Current state of the authentication process
    /// </summary>
    internal WinAuthState AuthState;

    /// <summary>
    ///     Context will be used to validate HTLM hashes
    /// </summary>
    internal Common.SecurityHandle Context;

    /// <summary>
    ///     Credentials used to validate NTLM hashes
    /// </summary>
    internal Common.SecurityHandle Credentials;

    /// <summary>
    ///     Timestamp needed to calculate validity of the authenticated session
    /// </summary>
    internal DateTime LastSeen;

    internal State ()
    {
        Credentials = new Common.SecurityHandle(0);
        Context = new Common.SecurityHandle(0);

        LastSeen = DateTime.UtcNow;
        AuthState = WinAuthState.Unauthorized;
    }

    internal void ResetHandles ()
    {
        Credentials.Reset();
        Context.Reset();
        AuthState = WinAuthState.Unauthorized;
    }

    internal void UpdatePresence ()
    {
        LastSeen = DateTime.UtcNow;
    }
}

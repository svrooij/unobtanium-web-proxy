namespace Titanium.Web.Proxy.Models;
/// <summary>
/// The ProxyAuthenticationResult enumeration represents the possible results of a proxy authentication attempt.
/// It provides values for indicating success, failure due to invalid credentials, and other potential outcomes.
/// </summary>
public enum ProxyAuthenticationResult
{
    /// <summary>
    /// Indicates the authentication request was successful.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates the authentication request failed.
    /// </summary>
    Failure,

    /// <summary>
    /// Indicates that this stage of the authentication request succeeded
    /// And a second pass of the handshake needs to occur
    /// </summary>
    ContinuationNeeded
}

/// <summary>
/// A context container for authentication flows
/// </summary>
public class ProxyAuthenticationContext
{
    /// <summary>
    ///     The result of the current authentication request
    /// </summary>
    public ProxyAuthenticationResult Result { get; set; }

    /// <summary>
    ///     An optional continuation token to return to the caller if set
    /// </summary>
    public string? Continuation { get; set; }

    /// <summary>
    /// Creates a new ProxyAuthenticationContext instance representing a failed authentication attempt.
    /// </summary>
    /// <returns>A new ProxyAuthenticationContext instance with Result set to Failure and Continuation set to null.</returns>
    public static ProxyAuthenticationContext Failed ()
    {
        return new ProxyAuthenticationContext
        {
            Result = ProxyAuthenticationResult.Failure,
            Continuation = null
        };
    }

    /// <summary>
    /// Creates a new ProxyAuthenticationContext instance representing a successful authentication attempt.
    /// </summary>
    /// <returns>A new ProxyAuthenticationContext instance with Result set to Success and Continuation set to null.</returns>
    public static ProxyAuthenticationContext Succeeded ()
    {
        return new ProxyAuthenticationContext
        {
            Result = ProxyAuthenticationResult.Success,
            Continuation = null
        };
    }
}
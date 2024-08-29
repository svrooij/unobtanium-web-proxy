using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Web.Proxy.EventArguments;

/// <summary>
///     An argument passed on to the user for validating the server certificate
///     during SSL authentication.
/// </summary>
public class CertificateValidationEventArgs : ProxyEventArgsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateValidationEventArgs"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="chain">The chain.</param>
    /// <param name="sslPolicyErrors">The SSL policy errors.</param>
    public CertificateValidationEventArgs ( SessionEventArgsBase session, X509Certificate certificate, X509Chain chain,
        SslPolicyErrors sslPolicyErrors ) : base(session.Server, session.ClientConnection)
    {
        Session = session;
        Certificate = certificate;
        Chain = chain;
        SslPolicyErrors = sslPolicyErrors;
    }

    /// <value>
    ///     The session.
    /// </value>
    public SessionEventArgsBase Session { get; }

    /// <summary>
    ///     Server certificate.
    /// </summary>
    public X509Certificate Certificate { get; }

    /// <summary>
    ///     Certificate chain.
    /// </summary>
    public X509Chain Chain { get; }

    /// <summary>
    ///     SSL policy errors.
    /// </summary>
    public SslPolicyErrors SslPolicyErrors { get; }

    /// <summary>
    ///     Is the given server certificate valid?
    /// </summary>
    public bool IsValid { get; set; }
}

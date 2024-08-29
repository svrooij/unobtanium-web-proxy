using System.Security.Cryptography.X509Certificates;

namespace Unobtanium.Web.Proxy.EventArguments;

/// <summary>
///     An argument passed on to user for client certificate selection during mutual SSL authentication.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CertificateSelectionEventArgs"/> class.
/// </remarks>
/// <param name="session">The session event arguments.</param>
/// <param name="targetHost">The target host.</param>
/// <param name="localCertificates">The local certificates.</param>
/// <param name="remoteCertificate">The remote certificate.</param>
/// <param name="acceptableIssuers">The acceptable issuers.</param>
public class CertificateSelectionEventArgs ( SessionEventArgsBase session, string targetHost,
    X509CertificateCollection localCertificates, X509Certificate? remoteCertificate, string[] acceptableIssuers ) : ProxyEventArgsBase(session.Server, session.ClientConnection)
{

    /// <value>
    ///     The session.
    /// </value>
    public SessionEventArgsBase Session { get; } = session;

    /// <summary>
    ///     The remote hostname to which we are authenticating against.
    /// </summary>
    public string TargetHost { get; } = targetHost;

    /// <summary>
    ///     Local certificates in store with matching issuers requested by TargetHost website.
    /// </summary>
    public X509CertificateCollection LocalCertificates { get; } = localCertificates;

    /// <summary>
    ///     Certificate of the remote server.
    /// </summary>
    public X509Certificate? RemoteCertificate { get; } = remoteCertificate;

    /// <summary>
    ///     Acceptable issuers as listed by remote server.
    /// </summary>
    public string[] AcceptableIssuers { get; } = acceptableIssuers;

    /// <summary>
    ///     Client Certificate we selected. Set this value to override.
    /// </summary>
    public X509Certificate? ClientCertificate { get; set; }
}

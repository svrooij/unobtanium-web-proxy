namespace Unobtanium.Web.Proxy;

public class ProxyServerOptions
{
    public int Port { get; set; }
    public int HttpsPort { get; set; }
    public bool SetAsSystemProxy { get; set; } = false;
    public bool TrustCertificateOnStart { get; set; } = false;
    public bool TrustCertificateOnStartAsUser { get; set; } = false;
    public string[]? PreloadCertificates { get; set; } = null;
}

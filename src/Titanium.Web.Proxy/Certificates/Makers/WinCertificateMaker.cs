using System;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.Network.Certificate;
// TODO FIX these warnings CS8600, CS8601, CS8618
//#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8618 // Dereference of a possibly null reference.

/// <inheritdoc />
/// <summary>
///     Certificate Maker - uses MakeCert
///     Calls COM objects using reflection
/// </summary>
internal class WinCertificateMaker : ICertificateMaker
{
    private readonly string sProviderName = "Microsoft Enhanced Cryptographic Provider v1.0";

    private readonly Type typeAltNamesCollection;

    private readonly Type typeBasicConstraints;

    private readonly Type typeCAlternativeName;

    private readonly Type typeEkuExt;

    private readonly Type typeExtNames;

    private readonly Type typeKuExt;

    private readonly Type typeOid;

    private readonly Type typeOids;

    private readonly Type typeRequestCert;

    private readonly Type typeSignerCertificate;
    private readonly Type typeX500Dn;

    private readonly Type typeX509Enrollment;

    private readonly Type typeX509Extensions;

    private readonly Type typeX509PrivateKey;

    // Validity Days for Root Certificates Generated.
    private readonly int certificateValidDays;

    private object? sharedPrivateKey;

    /// <summary>
    ///     Constructor.
    /// </summary>
    internal WinCertificateMaker ( int certificateValidDays )
    {
        this.certificateValidDays = certificateValidDays;

        typeX500Dn = Type.GetTypeFromProgID("X509Enrollment.CX500DistinguishedName", true);
        typeX509PrivateKey = Type.GetTypeFromProgID("X509Enrollment.CX509PrivateKey", true);
        typeOid = Type.GetTypeFromProgID("X509Enrollment.CObjectId", true);
        typeOids = Type.GetTypeFromProgID("X509Enrollment.CObjectIds.1", true);
        typeEkuExt = Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionEnhancedKeyUsage");
        typeKuExt = Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionKeyUsage");
        typeRequestCert = Type.GetTypeFromProgID("X509Enrollment.CX509CertificateRequestCertificate");
        typeX509Extensions = Type.GetTypeFromProgID("X509Enrollment.CX509Extensions");
        typeBasicConstraints = Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionBasicConstraints");
        typeSignerCertificate = Type.GetTypeFromProgID("X509Enrollment.CSignerCertificate");
        typeX509Enrollment = Type.GetTypeFromProgID("X509Enrollment.CX509Enrollment");

        // for alternative names
        typeAltNamesCollection = Type.GetTypeFromProgID("X509Enrollment.CAlternativeNames");
        typeExtNames = Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionAlternativeNames");
        typeCAlternativeName = Type.GetTypeFromProgID("X509Enrollment.CAlternativeName");
    }

    /// <summary>
    ///     Make certificate.
    /// </summary>
    public X509Certificate2 MakeCertificate ( string sSubjectCn, X509Certificate2? signingCert = null )
    {
        return MakeCertificate(sSubjectCn, true, signingCert);
    }

    private X509Certificate2 MakeCertificate ( string sSubjectCn,
        bool switchToMtaIfNeeded, X509Certificate2? signingCertificate = null,
        CancellationToken cancellationToken = default )
    {
        if (switchToMtaIfNeeded && Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
            return Task.Run(() => MakeCertificate(sSubjectCn, false, signingCertificate),
                cancellationToken).Result;

        // Subject
        var fullSubject = $"CN={sSubjectCn}";

        // Sig Algo
        const string hashAlgo = "SHA256";

        // Grace Days
        const int graceDays = -366;

        // KeyLength
        const int keyLength = 2048;

        var now = DateTime.UtcNow;
        var graceTime = now.AddDays(graceDays);
        var certificate = MakeCertificate(sSubjectCn, fullSubject, keyLength, hashAlgo, graceTime,
            now.AddDays(certificateValidDays), signingCertificate);
        return certificate;
    }

    private X509Certificate2 MakeCertificate ( string subject, string fullSubject,
        int privateKeyLength, string hashAlg, DateTime validFrom, DateTime validTo,
        X509Certificate2? signingCertificate )
    {
        var x500CertDn = Activator.CreateInstance(typeX500Dn);
        var typeValue = new object[] { fullSubject, 0 };
        typeX500Dn.InvokeMember("Encode", BindingFlags.InvokeMethod, null, x500CertDn, typeValue);

        var x500RootCertDn = Activator.CreateInstance(typeX500Dn);

        if (signingCertificate != null) typeValue[0] = signingCertificate.Subject;

        typeX500Dn.InvokeMember("Encode", BindingFlags.InvokeMethod, null, x500RootCertDn, typeValue);

        object? sharedPrivateKey = null;
        if (signingCertificate != null) sharedPrivateKey = this.sharedPrivateKey;

        if (sharedPrivateKey == null)
        {
            sharedPrivateKey = Activator.CreateInstance(typeX509PrivateKey);
            typeValue = [sProviderName];
            typeX509PrivateKey.InvokeMember("ProviderName", BindingFlags.PutDispProperty, null, sharedPrivateKey,
                typeValue);
            typeValue[0] = 2;
            typeX509PrivateKey.InvokeMember("ExportPolicy", BindingFlags.PutDispProperty, null, sharedPrivateKey,
                typeValue);
            typeValue = [signingCertificate == null ? 2 : 1];
            typeX509PrivateKey.InvokeMember("KeySpec", BindingFlags.PutDispProperty, null, sharedPrivateKey,
                typeValue);

            if (signingCertificate != null)
            {
                typeValue = [176];
                typeX509PrivateKey.InvokeMember("KeyUsage", BindingFlags.PutDispProperty, null, sharedPrivateKey,
                    typeValue);
            }

            typeValue[0] = privateKeyLength;
            typeX509PrivateKey.InvokeMember("Length", BindingFlags.PutDispProperty, null, sharedPrivateKey,
                typeValue);
            typeX509PrivateKey.InvokeMember("Create", BindingFlags.InvokeMethod, null, sharedPrivateKey, null);

            if (signingCertificate != null) this.sharedPrivateKey = sharedPrivateKey;
        }

        typeValue = new object[1];

        var oid = Activator.CreateInstance(typeOid);
        typeValue[0] = "1.3.6.1.5.5.7.3.1";
        typeOid.InvokeMember("InitializeFromValue", BindingFlags.InvokeMethod, null, oid, typeValue);

        var oids = Activator.CreateInstance(typeOids);
        typeValue[0] = oid;
        typeOids.InvokeMember("Add", BindingFlags.InvokeMethod, null, oids, typeValue);

        var ekuExt = Activator.CreateInstance(typeEkuExt);
        typeValue[0] = oids;
        typeEkuExt.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, ekuExt, typeValue);

        var requestCert = Activator.CreateInstance(typeRequestCert);

        typeValue = [1, sharedPrivateKey, string.Empty];
        typeRequestCert.InvokeMember("InitializeFromPrivateKey", BindingFlags.InvokeMethod, null, requestCert,
            typeValue);
        typeValue = [x500CertDn];
        typeRequestCert.InvokeMember("Subject", BindingFlags.PutDispProperty, null, requestCert, typeValue);
        typeValue[0] = x500RootCertDn;
        typeRequestCert.InvokeMember("Issuer", BindingFlags.PutDispProperty, null, requestCert, typeValue);
        typeValue[0] = validFrom;
        typeRequestCert.InvokeMember("NotBefore", BindingFlags.PutDispProperty, null, requestCert, typeValue);
        typeValue[0] = validTo;
        typeRequestCert.InvokeMember("NotAfter", BindingFlags.PutDispProperty, null, requestCert, typeValue);

        var kuExt = Activator.CreateInstance(typeKuExt);

        typeValue[0] = 176;
        typeKuExt.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, kuExt, typeValue);

        var certificate =
            typeRequestCert.InvokeMember("X509Extensions", BindingFlags.GetProperty, null, requestCert, null);
        typeValue = new object[1];

        if (signingCertificate != null)
        {
            typeValue[0] = kuExt;
            typeX509Extensions.InvokeMember("Add", BindingFlags.InvokeMethod, null, certificate, typeValue);
        }

        typeValue[0] = ekuExt;
        typeX509Extensions.InvokeMember("Add", BindingFlags.InvokeMethod, null, certificate, typeValue);

        if (signingCertificate != null)
        {
            // add alternative names
            // https://forums.iis.net/t/1180823.aspx

            var altNameCollection = Activator.CreateInstance(typeAltNamesCollection);
            var extNames = Activator.CreateInstance(typeExtNames);
            var altDnsNames = Activator.CreateInstance(typeCAlternativeName);

            if (IPAddress.TryParse(subject, out var ip))
            {
                var ipBase64 = Convert.ToBase64String(ip.GetAddressBytes());
                typeValue = [AlternativeNameType.XcnCertAltNameIpAddress, EncodingType.XcnCryptStringBase64, ipBase64];
                typeCAlternativeName.InvokeMember("InitializeFromRawData", BindingFlags.InvokeMethod, null, altDnsNames,
                    typeValue);
            }
            else
            {
                typeValue = [3, subject]; //3==DNS, 8==IP ADDR
                typeCAlternativeName.InvokeMember("InitializeFromString", BindingFlags.InvokeMethod, null, altDnsNames,
                    typeValue);
            }

            typeValue = [altDnsNames];
            typeAltNamesCollection.InvokeMember("Add", BindingFlags.InvokeMethod, null, altNameCollection,
                typeValue);

            typeValue = [altNameCollection];
            typeExtNames.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, extNames, typeValue);

            typeValue[0] = extNames;
            typeX509Extensions.InvokeMember("Add", BindingFlags.InvokeMethod, null, certificate, typeValue);
        }

        if (signingCertificate != null)
        {
            var signerCertificate = Activator.CreateInstance(typeSignerCertificate);

            typeValue = [0, 0, 12, signingCertificate.Thumbprint];
            typeSignerCertificate.InvokeMember("Initialize", BindingFlags.InvokeMethod, null, signerCertificate,
                typeValue);
            typeValue = [signerCertificate];
            typeRequestCert.InvokeMember("SignerCertificate", BindingFlags.PutDispProperty, null, requestCert,
                typeValue);
        }
        else
        {
            var basicConstraints = Activator.CreateInstance(typeBasicConstraints);

            typeValue = ["true", "0"];
            typeBasicConstraints.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, basicConstraints,
                typeValue);
            typeValue = [basicConstraints];
            typeX509Extensions.InvokeMember("Add", BindingFlags.InvokeMethod, null, certificate, typeValue);
        }

        oid = Activator.CreateInstance(typeOid);

        typeValue = [1, 0, 0, hashAlg];
        typeOid.InvokeMember("InitializeFromAlgorithmName", BindingFlags.InvokeMethod, null, oid, typeValue);

        typeValue = [oid];
        typeRequestCert.InvokeMember("HashAlgorithm", BindingFlags.PutDispProperty, null, requestCert, typeValue);
        typeRequestCert.InvokeMember("Encode", BindingFlags.InvokeMethod, null, requestCert, null);

        var x509Enrollment = Activator.CreateInstance(typeX509Enrollment);

        typeValue[0] = requestCert;
        typeX509Enrollment.InvokeMember("InitializeFromRequest", BindingFlags.InvokeMethod, null, x509Enrollment,
            typeValue);

        if (signingCertificate == null)
        {
            typeValue[0] = fullSubject;
            typeX509Enrollment.InvokeMember("CertificateFriendlyName", BindingFlags.PutDispProperty, null,
                x509Enrollment, typeValue);
        }

        typeValue[0] = 0;

        var createCertRequest = typeX509Enrollment.InvokeMember("CreateRequest", BindingFlags.InvokeMethod, null,
            x509Enrollment, typeValue);
        typeValue = [2, createCertRequest, 0, string.Empty];

        typeX509Enrollment.InvokeMember("InstallResponse", BindingFlags.InvokeMethod, null, x509Enrollment,
            typeValue);
        typeValue = [null!, 0, 1];

        var empty = (string?)typeX509Enrollment.InvokeMember("CreatePFX", BindingFlags.InvokeMethod, null,
            x509Enrollment, typeValue);

        return new X509Certificate2(Convert.FromBase64String(empty ?? string.Empty), string.Empty, X509KeyStorageFlags.Exportable);
    }
}

#pragma warning restore

/// <summary>
/// The EncodingType enumeration represents the different encoding types that can be used when creating a certificate.
/// </summary>
public enum EncodingType
{
    /// <summary>
    /// Represents any encoding type.
    /// </summary>
    XcnCryptStringAny = 7,

    /// <summary>
    /// Represents Base64 encoding.
    /// </summary>
    XcnCryptStringBase64 = 1,

    /// <summary>
    /// Represents any Base64 encoding.
    /// </summary>
    XcnCryptStringBase64Any = 6,

    /// <summary>
    /// Represents Base64 encoding with a header.
    /// </summary>
    XcnCryptStringBase64Header = 0,

    /// <summary>
    /// Represents Base64 encoding with a request header.
    /// </summary>
    XcnCryptStringBase64Requestheader = 3,

    /// <summary>
    /// Represents Base64 encoding for URIs.
    /// </summary>
    XcnCryptStringBase64Uri = 13,

    /// <summary>
    /// Represents Base64 encoding with a X509 CRL header.
    /// </summary>
    XcnCryptStringBase64X509Crlheader = 9,

    /// <summary>
    /// Represents binary encoding.
    /// </summary>
    XcnCryptStringBinary = 2,

    /// <summary>
    /// Represents chain encoding.
    /// </summary>
    XcnCryptStringChain = 0x100,

    /// <summary>
    /// Represents the encode mask.
    /// </summary>
    XcnCryptStringEncodemask = 0xff,

    /// <summary>
    /// Represents hash data encoding.
    /// </summary>
    XcnCryptStringHashdata = 0x10000000,

    /// <summary>
    /// Represents hexadecimal encoding.
    /// </summary>
    XcnCryptStringHex = 4,

    /// <summary>
    /// Represents any hexadecimal encoding.
    /// </summary>
    XcnCryptStringHexAny = 8,

    /// <summary>
    /// Represents hexadecimal address encoding.
    /// </summary>
    XcnCryptStringHexaddr = 10,

    /// <summary>
    /// Represents hexadecimal ASCII encoding.
    /// </summary>
    XcnCryptStringHexascii = 5,

    /// <summary>
    /// Represents hexadecimal ASCII address encoding.
    /// </summary>
    XcnCryptStringHexasciiaddr = 11,

    /// <summary>
    /// Represents raw hexadecimal encoding.
    /// </summary>
    XcnCryptStringHexraw = 12,

    /// <summary>
    /// Represents encoding with no carriage return.
    /// </summary>
    XcnCryptStringNocr = -2147483648,

    /// <summary>
    /// Represents encoding with no CRLF.
    /// </summary>
    XcnCryptStringNocrlf = 0x40000000,

    /// <summary>
    /// Represents percent escape encoding.
    /// </summary>
    XcnCryptStringPercentescape = 0x8000000,

    /// <summary>
    /// Represents strict encoding.
    /// </summary>
    XcnCryptStringStrict = 0x20000000,

    /// <summary>
    /// Represents text encoding.
    /// </summary>
    XcnCryptStringText = 0x200
}

/// <summary>
/// The AlternativeNameType enumeration represents the different types of alternative names that can be used in a certificate.
/// </summary>
public enum AlternativeNameType
{
    /// <summary>
    /// Represents a directory name.
    /// </summary>
    XcnCertAltNameDirectoryName = 5,

    /// <summary>
    /// Represents a DNS name.
    /// </summary>
    XcnCertAltNameDnsName = 3,

    /// <summary>
    /// Represents a GUID.
    /// </summary>
    XcnCertAltNameGuid = 10,

    /// <summary>
    /// Represents an IP address.
    /// </summary>
    XcnCertAltNameIpAddress = 8,

    /// <summary>
    /// Represents an other name.
    /// </summary>
    XcnCertAltNameOtherName = 1,

    /// <summary>
    /// Represents a registered ID.
    /// </summary>
    XcnCertAltNameRegisteredId = 9,

    /// <summary>
    /// Represents an RFC 822 name.
    /// </summary>
    XcnCertAltNameRfc822Name = 2,

    /// <summary>
    /// Represents an unknown name type.
    /// </summary>
    XcnCertAltNameUnknown = 0,

    /// <summary>
    /// Represents a URL.
    /// </summary>
    XcnCertAltNameUrl = 7,

    /// <summary>
    /// Represents a user principle name.
    /// </summary>
    XcnCertAltNameUserPrincipleName = 11
}

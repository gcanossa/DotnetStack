using System.Security.Cryptography.X509Certificates;

namespace GKit.RENTRI;

public class ClientOptions
{
    public required string Audience { get; set; }
    public required X509Certificate2 Certificate { get; set; }
    public required string BaseUrl { get; set; } = "";
    public required string Issuer { get; set; }

    public static ClientOptions CreateLiveOption(string issuer, string certFilePath, string certPassword)
    {
        return new ClientOptions()
        {
            Issuer = issuer,
            Audience = "",
            BaseUrl = "",
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certFilePath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet),
        }.AsLive();
    }

    public static ClientOptions CreateDemoOption(string issuer, string certFilePath, string certPassword)
    {
        return new ClientOptions()
        {
            Issuer = issuer,
            Audience = "",
            BaseUrl = "",
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certFilePath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet),
        }.AsDemo();
    }
}
using System.Security.Cryptography.X509Certificates;

namespace GKit.RENTRI;

public class ClientOptions
{
    public required string Audience { get; set; }
    public required X509Certificate2 Certificate { get; set; }
    public required string BaseUrl { get; set; } = "";
    public required string Issuer { get; set; }
}
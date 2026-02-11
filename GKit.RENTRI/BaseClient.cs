using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GKit.RENTRI;

public abstract class BaseClient : IDisposable
{
    public ClientOptions Options { get; init; } = null!;

    protected string GetAlgorithm(X509Certificate2 certificate)
    {
        return certificate.PublicKey.Oid.FriendlyName switch
        {
            "RSA" => SecurityAlgorithms.RsaSha256,
            "ECC" => SecurityAlgorithms.EcdsaSha256,
            _ => throw new InvalidOperationException("Unsupported key algorithm")
        };
    }

    protected SigningCredentials GetSigningCredentials(X509Certificate2 certificate)
    {
        var algorithm = GetAlgorithm(certificate);
        return algorithm == SecurityAlgorithms.RsaSha256
            ? new SigningCredentials(new RsaSecurityKey(certificate.GetRSAPrivateKey()), algorithm)
            : new SigningCredentials(new ECDsaSecurityKey(certificate.GetECDsaPrivateKey()), algorithm);
    }

    protected SecurityTokenDescriptor CreateBaseTokenDescriptor(X509Certificate2 certificate)
    {
        return new SecurityTokenDescriptor
        {
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { "x5c", new[] { Convert.ToBase64String(certificate.Export(X509ContentType.Cert)) } }
            },
            Audience = Options.Audience,
            Issuer = Options.Issuer,
            Claims = new Dictionary<string, object>
            {
                { "jti", Guid.NewGuid().ToString() }
            },
            SigningCredentials = GetSigningCredentials(certificate)
        };
    }

    private string? IdAuthToken { get; set; }

    private string GetIdAuthJwt()
    {
        if (string.IsNullOrEmpty(IdAuthToken))
        {
            var tokenHandler = new JsonWebTokenHandler();

            IdAuthToken = tokenHandler.CreateToken(CreateBaseTokenDescriptor(Options.Certificate));
        }

        ;

        return IdAuthToken;
    }

    private record IntegrityValues(string Signature, string Digest);

    private IntegrityValues CreateIntegrityJwt(HttpContent content)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var tokenDescriptor = CreateBaseTokenDescriptor(Options.Certificate);

        using var sha256 = SHA256.Create();
        var digest = $"SHA-256={Convert.ToBase64String(
            sha256.ComputeHash(
                content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult()))}";

        tokenDescriptor.Claims.Add("signed_headers", new Dictionary<string, string>[]
        {
            new() { { "digest", digest } },
            new() { { "content-type", content.Headers.ContentType?.ToString()! } }
        });

        return new IntegrityValues(tokenHandler.CreateToken(tokenDescriptor), digest);
    }

    protected void AddAuthToHttpRequestMessage(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"Bearer {GetIdAuthJwt()}");
    }

    protected void AddIntegrityHttpRequestMessage(HttpRequestMessage request)
    {
        var integrity = CreateIntegrityJwt(request.Content!);

        request.Headers.Add("Digest", integrity.Digest);
        request.Headers.Add("Agid-JWT-Signature", integrity.Signature);
    }

    public abstract void Dispose();
}
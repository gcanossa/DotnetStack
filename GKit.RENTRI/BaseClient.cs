using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GKit.RENTRI;

public abstract class BaseClient
{
    protected abstract X509Certificate2 Certificate { get; }
    protected string Algorithm => Certificate.PublicKey.Oid.FriendlyName switch
    {
        "RSA" => SecurityAlgorithms.RsaSha256,
        "ECC" => SecurityAlgorithms.EcdsaSha256,
        _ => throw new InvalidOperationException("Unsupported key algorithm")
    };

    protected SigningCredentials SigningCredentials => Algorithm == SecurityAlgorithms.RsaSha256
        ? new SigningCredentials(new RsaSecurityKey(Certificate.GetRSAPrivateKey()), Algorithm)
        : new SigningCredentials(new ECDsaSecurityKey(Certificate.GetECDsaPrivateKey()), Algorithm);
    
    protected abstract string Issuer { get; }
    protected abstract string Audience { get; }

    protected string generateIdAuthJWT()
    {
        var tokenHandler = new JsonWebTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            AdditionalHeaderClaims = new Dictionary<string, object> { { "x5c", new string[] { Convert.ToBase64String(Certificate.Export(X509ContentType.Cert)) } } },
            Audience = Audience,
            Issuer = Issuer,
            Claims = new Dictionary<string, object> { { "jti",  Guid.NewGuid().ToString() } },
            SigningCredentials = SigningCredentials
        };
        var idAuth = tokenHandler.CreateToken(tokenDescriptor);
        
        return idAuth;
    }

    protected string generateIntegrityJWT(StringContent content)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            AdditionalHeaderClaims = new Dictionary<string, object> { { "x5c", new string[] { Convert.ToBase64String(Certificate.Export(X509ContentType.Cert)) } } },
            Audience = Audience,
            Issuer = Issuer,
            Claims = new Dictionary<string, object> { { "jti",  Guid.NewGuid().ToString() } },
            SigningCredentials = SigningCredentials
        };
        
        using var sha256 = SHA256.Create();
        var digest = $"SHA-256={Convert.ToBase64String(sha256.ComputeHash(await content.ReadAsByteArrayAsync()))}";

        tokenDescriptor.Claims.Add("signed_headers", new Dictionary<string, string>[] {
            new() { { "digest", digest } },
            new() { { "content-type", content.Headers.ContentType?.ToString()! } }
        });

        var integrity = tokenHandler.CreateToken(tokenDescriptor);
        return integrity;
    }
    
    private tmp()
    {
        // Configurazione
var p12 = "XXX"; // Base64 file .p12
var password = "XXX"; // Password del file .p12
var cert = new X509Certificate2(Convert.FromBase64String(p12), password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
var algo = cert.PublicKey.Oid.FriendlyName == "RSA" ? SecurityAlgorithms.RsaSha256 : cert.PublicKey.Oid.FriendlyName == "ECC" ? SecurityAlgorithms.EcdsaSha256 : throw new InvalidOperationException("Unsupported key algorithm");

var issuer = "XXX"; // Indicare l'identificativo dell'operatore presente nel subject del certificato
var regId = "XXX"; // Indicare l'identificativo del registro

var aud = "rentrigov.demo.api"; // Per produzione rentrigov.api
var baseApi = "https://demoapi.rentri.gov.it"; // Per produzione https://api.rentri.gov.it
var api = $"{baseApi}/dati-registri/v1.0/operatore/{regId}/movimenti";
var jti = Guid.NewGuid().ToString(); // Id del JWT

var jsonData = @"[{""riferimenti"": { ""numero_registrazione"": { ""anno"": 2024, ""progressivo"": 1 } }}]";

// Dati scambiati
var content = new StringContent(jsonData, System.Text.Encoding.UTF8, "application/json");

// ID_AUTH_REST_02
var tokenHandler = new JsonWebTokenHandler();
var tokenDescriptor = new SecurityTokenDescriptor
{
    AdditionalHeaderClaims = new Dictionary<string, object> { { "x5c", new string[] { Convert.ToBase64String(cert.Export(X509ContentType.Cert)) } } },
    Audience = aud,
    Issuer = issuer,
    Claims = new Dictionary<string, object> { { "jti", jti } },
    SigningCredentials = algo == SecurityAlgorithms.RsaSha256 ? new SigningCredentials(new RsaSecurityKey(cert.GetRSAPrivateKey()), algo) : new SigningCredentials(new ECDsaSecurityKey(cert.GetECDsaPrivateKey()), algo)
};
var idAuth = tokenHandler.CreateToken(tokenDescriptor);

// INTEGRITY_REST_01
using var sha256 = SHA256.Create();
var digest = $"SHA-256={Convert.ToBase64String(sha256.ComputeHash(await content.ReadAsByteArrayAsync()))}";

tokenDescriptor.Claims.Add("signed_headers", new Dictionary<string, string>[] {
    new() { { "digest", digest } },
    new() { { "content-type", content.Headers.ContentType?.ToString()! } }
});

var integrity = tokenHandler.CreateToken(tokenDescriptor);

// Client con headers
using var cli = new HttpClient();
cli.DefaultRequestHeaders.Add("Authorization", $"Bearer {idAuth}");
cli.DefaultRequestHeaders.Add("Digest", digest);
cli.DefaultRequestHeaders.Add("Agid-JWT-Signature", integrity);

// Chiamata API
var res = await cli.PostAsync(api, content);
var response = await res.Content.ReadAsStringAsync();
Console.WriteLine(response);
    }
}
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

    public Context? CurrentContext { get; protected set; }
    private readonly Lock _lock = new();

    private  readonly SemaphoreSlim _contextSemaphore = new(1, 1);
    public Context UseContext()
    {
        _contextSemaphore.Wait();
        CurrentContext = new Context(_contextSemaphore);
        return CurrentContext;
    }

    public async Task<T> WithContext<T>(Func<Context, Task<T>> func)
    {
        using var ctx = UseContext();

        return await func(ctx);
    }

    protected void ApplyPagingHeadersToContext(HttpResponseMessage response)
    {
        if(CurrentContext == null) return;

        CurrentContext.PageSize = !response.Headers.Contains("Paging-PageSize") ? 0 :
            Convert.ToInt32(response.Headers.GetValues("Paging-PageSize").First());
        CurrentContext.PageCount = !response.Headers.Contains("Paging-PageCount") ? 0 :
            Convert.ToInt32(response.Headers.GetValues("Paging-PageCount").First());
        CurrentContext.PageNumber = !response.Headers.Contains("Paging-Page") ? 0 :
            Convert.ToInt32(response.Headers.GetValues("Paging-Page").First());
        CurrentContext.TotalItems = !response.Headers.Contains("Paging-TotalRecordCount") ? 0 :
            Convert.ToInt32(response.Headers.GetValues("Paging-TotalRecordCount").First());

        CurrentContext.RetryAfter = !response.Headers.Contains("Retry-After") ? null :
            TimeSpan.Parse(response.Headers.GetValues("Retry-After").First());
    }

    public class Context(SemaphoreSlim semaphore) : IDisposable
    {
        public int? PageSize { get; set; }
        public int? PageCount { get; set; }
        public int? PageNumber { get; set; }
        public int? TotalItems { get; set; }
        public TimeSpan? RetryAfter { get; set; }
        public void Dispose()
        {
            semaphore.Release();
        }
    }

    internal Action<HttpClient, HttpRequestMessage, string>? PrepareRequestHandler;

    protected void OnPrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        AddAuthToHttpRequestMessage(request);

        if (request.Content is not null)
            AddIntegrityHttpRequestMessage(request);

        PrepareRequestHandler?.Invoke(client, request, url);
    }

    internal Action<HttpClient, HttpResponseMessage>? ProcessResponseHandler;
    protected void OnProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        ApplyPagingHeadersToContext(response);

        ProcessResponseHandler?.Invoke(client, response);
    }
}
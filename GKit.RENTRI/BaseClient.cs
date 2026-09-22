using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GKit.RENTRI;

public abstract class BaseClient : IDisposable
{
    public ClientOptions? Options { get; init; }

    /// <summary>
    /// Points a generated stub URL at the configured environment. When <paramref name="options"/>
    /// is null (an anonymous status client) <paramref name="anonymousBaseUrl"/> supplies the
    /// environment instead — previously that case fell through and kept the stub's hard-coded
    /// production host.
    /// </summary>
    protected static string ResolveBaseUrl(string generatedUrl, ClientOptions? options, string? anonymousBaseUrl)
    {
        var target = options?.BaseUrl ?? anonymousBaseUrl;

        return string.IsNullOrWhiteSpace(target)
            ? generatedUrl
            : RentriEndpoints.Rebase(generatedUrl, target);
    }

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

    /// <summary>How long a generated RENTRI token stays valid. Kept short by design.</summary>
    protected virtual TimeSpan TokenLifetime => TimeSpan.FromMinutes(5);

    protected SecurityTokenDescriptor CreateBaseTokenDescriptor(X509Certificate2 certificate)
    {
        var options = Options ?? throw new InvalidOperationException(
            "This client was created without ClientOptions and cannot sign a request.");

        var issuedAt = DateTime.UtcNow;

        return new SecurityTokenDescriptor
        {
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { "x5c", new[] { Convert.ToBase64String(certificate.Export(X509ContentType.Cert)) } }
            },
            Audience = options.Audience,
            Issuer = options.Issuer,
            // Set explicitly: JsonWebTokenHandler otherwise applies its own 60-minute default,
            // which the Agid-JWT-Signature profile does not expect.
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.Add(TokenLifetime),
            Claims = new Dictionary<string, object>
            {
                { "jti", Guid.NewGuid().ToString() }
            },
            SigningCredentials = GetSigningCredentials(certificate)
        };
    }

    private string GetIdAuthJwt()
    {
        var tokenHandler = new JsonWebTokenHandler();

        return tokenHandler.CreateToken(CreateBaseTokenDescriptor(Options!.Certificate));
    }

    private record IntegrityValues(string Signature, string Digest);

    private IntegrityValues CreateIntegrityJwt(HttpContent content)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var tokenDescriptor = CreateBaseTokenDescriptor(Options!.Certificate);

        // PrepareRequest is a synchronous partial on the generated stub, so this cannot be
        // awaited without editing generated code. ReadAsStream is genuinely synchronous rather
        // than a blocking wait on a Task — valid because NSwag emits buffered content
        // (StringContent / ByteArrayContent), whose read stream is independent of the stream
        // HttpClient later serialises.
        var digest = $"SHA-256={Convert.ToBase64String(SHA256.HashData(content.ReadAsStream()))}";

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

    private readonly SemaphoreSlim _contextSemaphore = new(1, 1);

    /// <summary>
    /// Blocking. Prefer <see cref="UseContextAsync"/>: this holds a thread pool thread for the
    /// whole duration of the enclosed remote call.
    /// </summary>
    public Context UseContext()
    {
        _contextSemaphore.Wait();
        CurrentContext = new Context(_contextSemaphore);
        return CurrentContext;
    }

    public async Task<Context> UseContextAsync(CancellationToken cancellationToken = default)
    {
        await _contextSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        CurrentContext = new Context(_contextSemaphore);
        return CurrentContext;
    }

    public async Task<T> WithContext<T>(Func<Context, Task<T>> func, CancellationToken cancellationToken = default)
    {
        using var ctx = await UseContextAsync(cancellationToken).ConfigureAwait(false);

        return await func(ctx).ConfigureAwait(false);
    }

    public async Task WithContext(Func<Context, Task> func, CancellationToken cancellationToken = default)
    {
        using var ctx = await UseContextAsync(cancellationToken).ConfigureAwait(false);

        await func(ctx).ConfigureAwait(false);
    }

    protected void ApplyPagingHeadersToContext(HttpResponseMessage response)
    {
        if (CurrentContext == null) return;

        CurrentContext.PageSize = ParseHeaderInt(response, "Paging-PageSize");
        CurrentContext.PageCount = ParseHeaderInt(response, "Paging-PageCount");
        CurrentContext.PageNumber = ParseHeaderInt(response, "Paging-Page");
        CurrentContext.TotalItems = ParseHeaderInt(response, "Paging-TotalRecordCount");

        CurrentContext.RetryAfter = ParseRetryAfter(response);
    }

    /// <summary>
    /// Header values are machine-to-machine and must not follow the server's culture.
    /// Returns 0 when the header is absent or unparseable, matching the previous behaviour.
    /// </summary>
    internal static int ParseHeaderInt(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values)) return 0;

        return int.TryParse(values.FirstOrDefault(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    /// <summary>
    /// RFC 9110 Retry-After is either delta-seconds ("120") or an HTTP-date, never a TimeSpan
    /// literal: <c>TimeSpan.Parse("120")</c> yields <b>120 days</b>, so any retry honouring the
    /// old value stalled indefinitely. HttpResponseHeaders.RetryAfter models both forms.
    /// </summary>
    internal static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;

        if (retryAfter.Delta is { } delta) return delta;

        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
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

    /// <summary>Extension point for callers that need to decorate outbound requests.</summary>
    public Action<HttpClient, HttpRequestMessage, string>? PrepareRequestHandler { get; set; }

    protected void OnPrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        if(Options is not null)
        {
            AddAuthToHttpRequestMessage(request);

            if (request.Content is not null)
                AddIntegrityHttpRequestMessage(request);
        }

        PrepareRequestHandler?.Invoke(client, request, url);
    }

    /// <summary>Extension point invoked after every response; used for API status tracking.</summary>
    public Action<HttpClient, HttpResponseMessage>? ProcessResponseHandler { get; set; }

    protected void OnProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        ApplyPagingHeadersToContext(response);

        ProcessResponseHandler?.Invoke(client, response);
    }
}
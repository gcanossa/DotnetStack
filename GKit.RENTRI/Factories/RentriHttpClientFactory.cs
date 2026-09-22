using Microsoft.Extensions.DependencyInjection;

namespace GKit.RENTRI;

public static class RentriHttpClientFactory
{
    public const string ClientName = "GKit.RENTRI";

    /// <summary>
    /// Registers the pooled <see cref="HttpClient"/> the RENTRI factories resolve.
    /// <para>
    /// Every lookup previously constructed its own <see cref="HttpClient"/> and
    /// <c>HttpClientHandler</c> and disposed them, leaving a socket in TIME_WAIT per call. Under
    /// the per-request call volume of a Blazor app that exhausts the ephemeral port range.
    /// <see cref="IHttpClientFactory"/> pools and rotates the handler instead.
    /// </para>
    /// </summary>
    public static IServiceCollection AddRentriHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            // The handler is pooled and rotated by the factory, so it must outlive the
            // HttpClient instances that the generated stubs dispose.
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}

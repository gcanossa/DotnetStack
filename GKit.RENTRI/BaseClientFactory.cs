using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public abstract class BaseClientFactory<T> where T : BaseClient
{
    private readonly ApiStatusProvider apiStatusProvider;
    private readonly IOptions<RentriOptions> rentriOptions;

    protected BaseClientFactory(ApiStatusProvider apiStatusProvider, IOptions<RentriOptions> rentriOptions)
    {
        this.apiStatusProvider = apiStatusProvider;
        this.rentriOptions = rentriOptions;
    }

    /// <summary>Identifies which <see cref="ApiStatusProvider"/> slot this client reports into.</summary>
    protected abstract RentriApi Api { get; }

    protected abstract T BuildClient(ClientOptions? options, string? anonymousBaseUrl);

    private T Configure(T client)
    {
        client.ProcessResponseHandler = (_, response) =>
            apiStatusProvider.Set(Api, ApiStatusProvider.GetApiStatusFromHttpStatusCode((int)response.StatusCode));

        return client;
    }

    public T CreateClient(ClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Configure(BuildClient(options, null));
    }

    /// <summary>
    /// A client with no certificate, for the unauthenticated status endpoints.
    /// <para>
    /// The environment comes from <see cref="RentriOptions"/>: previously this passed a null
    /// <see cref="ClientOptions"/>, which skipped the base-URL rewrite entirely and left the
    /// probes hitting production even for a demo-configured application.
    /// </para>
    /// </summary>
    public T CreateAnonymousClient()
    {
        return Configure(BuildClient(null, RentriEndpoints.BaseUrlFor(rentriOptions.Value.Environment)));
    }
}

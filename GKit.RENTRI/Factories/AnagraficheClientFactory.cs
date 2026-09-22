using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class AnagraficheClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<AnagraficheClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.Anagrafiche;

    protected override AnagraficheClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new AnagraficheClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

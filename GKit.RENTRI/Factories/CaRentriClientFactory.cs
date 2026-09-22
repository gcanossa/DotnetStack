using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class CaRentriClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<CaRentriClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.CaRentri;

    protected override CaRentriClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new CaRentriClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

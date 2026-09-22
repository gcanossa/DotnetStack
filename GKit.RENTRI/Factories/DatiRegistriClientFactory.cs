using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class DatiRegistriClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<DatiRegistriClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.DatiRegistri;

    protected override DatiRegistriClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new DatiRegistriClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

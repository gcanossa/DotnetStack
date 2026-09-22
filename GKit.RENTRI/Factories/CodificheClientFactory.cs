using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class CodificheClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<CodificheClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.Codifiche;

    protected override CodificheClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new CodificheClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class FormulariClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<FormulariClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.Formulari;

    protected override FormulariClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new FormulariClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

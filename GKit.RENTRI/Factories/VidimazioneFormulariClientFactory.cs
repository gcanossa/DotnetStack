using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class VidimazioneFormulariClientFactory(
    ApiStatusProvider apiStatusProvider,
    IOptions<RentriOptions> rentriOptions,
    IHttpClientFactory httpClientFactory)
    : BaseClientFactory<VidimazioneFormulariClient>(apiStatusProvider, rentriOptions)
{
    protected override RentriApi Api => RentriApi.VidimazioneFormulari;

    protected override VidimazioneFormulariClient BuildClient(ClientOptions? options, string? anonymousBaseUrl)
    {
        return new VidimazioneFormulariClient(
            httpClientFactory.CreateClient(RentriHttpClientFactory.ClientName), options, anonymousBaseUrl);
    }
}

using GKit.RENTRI.Stubs.VidimazioneFormulari;

namespace GKit.RENTRI;

public class VidimazioneFormulariClient : VidimazioneFormulariStub
{
    public VidimazioneFormulariClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

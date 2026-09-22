using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClient : CodificheStub
{
    public CodificheClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

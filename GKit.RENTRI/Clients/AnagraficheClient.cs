using GKit.RENTRI.Stubs.Anagrafiche;

namespace GKit.RENTRI;

public class AnagraficheClient : AnagraficheStub
{
    public AnagraficheClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

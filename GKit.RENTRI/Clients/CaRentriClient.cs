using GKit.RENTRI.Stubs.CaRentri;

namespace GKit.RENTRI;

public class CaRentriClient : CaRentriStub
{
    public CaRentriClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

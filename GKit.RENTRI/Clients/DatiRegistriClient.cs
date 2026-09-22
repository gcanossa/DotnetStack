using GKit.RENTRI.Stubs.DatiRegistri;

namespace GKit.RENTRI;

public class DatiRegistriClient : DatiRegistriStub
{
    public DatiRegistriClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

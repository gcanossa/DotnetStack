using GKit.RENTRI.Stubs.DatiRegistri;

namespace GKit.RENTRI;

public class DatiRegistriClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider) : BaseClientFactory<DatiRegistriClient>(apiStatusProvider)
{
    protected override DatiRegistriClient BuildClient()
    {
        return new DatiRegistriClient(new HttpClient(), options);
    }
}
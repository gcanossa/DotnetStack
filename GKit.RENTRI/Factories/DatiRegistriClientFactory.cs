using GKit.RENTRI.Stubs.DatiRegistri;

namespace GKit.RENTRI;

public class DatiRegistriClientFactory(ApiStatusProvider apiStatusProvider) : BaseClientFactory<DatiRegistriClient>(apiStatusProvider)
{
    protected override DatiRegistriClient BuildClient(ClientOptions options)
    {
        return new DatiRegistriClient(RentriHttpClientFactory.Create(), options);
    }
}
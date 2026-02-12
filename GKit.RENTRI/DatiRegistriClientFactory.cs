using GKit.RENTRI.Stubs.DatiRegistri;

namespace GKit.RENTRI;

public class DatiRegistriClientFactory(ClientOptions options)
{
    public DatiRegistriClient CreateClient()
    {
        return new DatiRegistriClient(new HttpClient(), options);
    }
}
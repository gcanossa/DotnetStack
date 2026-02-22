using GKit.RENTRI.Stubs.DatiRegistri;

namespace GKit.RENTRI;

public class DatiRegistriClient : DatiRegistriStub
{
    public DatiRegistriClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        if(Options is not null)
        {
            BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
        }
    }
}
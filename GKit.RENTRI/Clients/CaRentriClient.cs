using GKit.RENTRI.Stubs.CaRentri;

namespace GKit.RENTRI;

public class CaRentriClient : CaRentriStub
{
    public CaRentriClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
    }
}
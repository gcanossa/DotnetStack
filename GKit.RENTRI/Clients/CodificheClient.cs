using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClient : CodificheStub
{
    public CodificheClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
    }
}
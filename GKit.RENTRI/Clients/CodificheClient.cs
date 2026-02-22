using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClient : CodificheStub
{
    public CodificheClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        if(Options is not null)
        {
            BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
        }
    }
}
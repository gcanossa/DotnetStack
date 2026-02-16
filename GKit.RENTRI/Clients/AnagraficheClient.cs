using GKit.RENTRI.Stubs.Anagrafiche;

namespace GKit.RENTRI;

public class AnagraficheClient : AnagraficheStub
{
    public AnagraficheClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
    }
}
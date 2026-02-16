using GKit.RENTRI.Stubs.Formulari;

namespace GKit.RENTRI;

public class FormulariClient : FormulariStub
{
    public FormulariClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
    }
}
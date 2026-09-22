using GKit.RENTRI.Stubs.Formulari;

namespace GKit.RENTRI;

public class FormulariClient : FormulariStub
{
    public FormulariClient(HttpClient httpClient, ClientOptions? options, string? anonymousBaseUrl = null)
        : base(httpClient)
    {
        Options = options;
        BaseUrl = ResolveBaseUrl(BaseUrl, options, anonymousBaseUrl);
    }
}

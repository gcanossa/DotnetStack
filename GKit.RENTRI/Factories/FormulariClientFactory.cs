using GKit.RENTRI.Stubs.Formulari;

namespace GKit.RENTRI;

public class FormulariClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider) : BaseClientFactory<FormulariClient>(apiStatusProvider)
{
    protected override FormulariClient BuildClient()
    {
        return new FormulariClient(new HttpClient(), options);
    }
}
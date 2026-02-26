using GKit.RENTRI.Stubs.Formulari;

namespace GKit.RENTRI;

public class FormulariClientFactory(ApiStatusProvider apiStatusProvider) : BaseClientFactory<FormulariClient>(apiStatusProvider)
{
    protected override FormulariClient BuildClient(ClientOptions options)
    {
        return new FormulariClient(RentriHttpClientFactory.Create(), options);
    }
}
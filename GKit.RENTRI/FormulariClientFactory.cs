using GKit.RENTRI.Stubs.Formulari;

namespace GKit.RENTRI;

public class FormulariClientFactory(ClientOptions options)
{
    public FormulariClient CreateClient()
    {
        return new FormulariClient(new HttpClient(), options);
    }
}
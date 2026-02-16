using GKit.RENTRI.Stubs.Anagrafiche;

namespace GKit.RENTRI;

public class AnagraficheClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider) : BaseClientFactory<AnagraficheClient>(apiStatusProvider)
{
    protected override AnagraficheClient BuildClient()
    {
        return new AnagraficheClient(new HttpClient(), options);
    }
}
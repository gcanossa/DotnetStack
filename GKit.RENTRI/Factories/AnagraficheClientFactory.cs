using GKit.RENTRI.Stubs.Anagrafiche;

namespace GKit.RENTRI;

public class AnagraficheClientFactory(ApiStatusProvider apiStatusProvider) : BaseClientFactory<AnagraficheClient>(apiStatusProvider)
{
    protected override AnagraficheClient BuildClient(ClientOptions options)
    {
        return new AnagraficheClient(RentriHttpClientFactory.Create(), options);
    }
}
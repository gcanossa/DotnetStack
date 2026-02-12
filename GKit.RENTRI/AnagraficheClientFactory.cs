using GKit.RENTRI.Stubs.Anagrafiche;

namespace GKit.RENTRI;

public class AnagraficheClientFactory(ClientOptions options)
{
    public AnagraficheClient CreateClient()
    {
        return new AnagraficheClient(new HttpClient(), options);
    }
}
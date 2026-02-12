using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClientFactory(ClientOptions options)
{
    public CodificheClient CreateClient()
    {
        return new CodificheClient(new HttpClient(), options);
    }
}
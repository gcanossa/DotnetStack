using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider) : BaseClientFactory<CodificheClient>(apiStatusProvider)
{
    protected override CodificheClient BuildClient()
    {
        return new CodificheClient(new HttpClient(), options);
    }
}
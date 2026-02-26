using GKit.RENTRI.Stubs.Codifiche;

namespace GKit.RENTRI;

public class CodificheClientFactory(ApiStatusProvider apiStatusProvider) : BaseClientFactory<CodificheClient>(apiStatusProvider)
{
    protected override CodificheClient BuildClient(ClientOptions options)
    {
        return new CodificheClient(RentriHttpClientFactory.Create(), options);
    }
}
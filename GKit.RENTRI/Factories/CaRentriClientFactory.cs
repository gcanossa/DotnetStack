using GKit.RENTRI.Stubs.CaRentri;

namespace GKit.RENTRI;

public class CaRentriClientFactory(ApiStatusProvider apiStatusProvider) : BaseClientFactory<CaRentriClient>(apiStatusProvider)
{
    protected override CaRentriClient BuildClient(ClientOptions options)
    {
        return new CaRentriClient(new HttpClient(), options);
    }
}
using GKit.RENTRI.Stubs.CaRentri;

namespace GKit.RENTRI;

public class CaRentriClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider) : BaseClientFactory<CaRentriClient>(apiStatusProvider)
{
    protected override CaRentriClient BuildClient()
    {
        return new CaRentriClient(new HttpClient(), options);
    }
}
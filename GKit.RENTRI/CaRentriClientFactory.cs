using GKit.RENTRI.Stubs.CaRentri;

namespace GKit.RENTRI;

public class CaRentriClientFactory(ClientOptions options)
{
    public CaRentriClient CreateClient()
    {
        return new CaRentriClient(new HttpClient(), options);
    }
}
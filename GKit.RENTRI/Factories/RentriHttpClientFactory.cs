namespace GKit.RENTRI;

public static class RentriHttpClientFactory
{
    public static HttpClient Create()
    {
        return new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = false,
        });
    }
}
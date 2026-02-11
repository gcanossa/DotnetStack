namespace GKit.RENTRI.Stubs.CaRentri;

public partial class CaRentriStub : BaseClient
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        AddAuthToHttpRequestMessage(request);

        if (request.Content is not null)
            AddIntegrityHttpRequestMessage(request);
    }
}
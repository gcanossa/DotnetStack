namespace GKit.RENTRI.Stubs.Anagrafiche;

public partial class AnagraficheStub : BaseClient
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        AddAuthToHttpRequestMessage(request);

        if (request.Content is not null)
            AddIntegrityHttpRequestMessage(request);
    }
}
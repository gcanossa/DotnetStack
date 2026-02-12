namespace GKit.RENTRI.Stubs.Anagrafiche;

public partial class AnagraficheStub : BaseClient
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        AddAuthToHttpRequestMessage(request);

        if (request.Content is not null)
            AddIntegrityHttpRequestMessage(request);
    }

    partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        ApplyPagingHeadersToContext(response);
    }

    private bool _diposed = false;
    public override void Dispose()
    {
        if(!_diposed)
        {
            _httpClient?.Dispose();
            _diposed = true;
        }
    }
}
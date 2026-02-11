namespace GKit.RENTRI.Stubs.VidimazioneFormulari;

public partial class VidimazioneFormulariStub : BaseClient
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        AddAuthToHttpRequestMessage(request);

        if (request.Content is not null)
            AddIntegrityHttpRequestMessage(request);
    }
}
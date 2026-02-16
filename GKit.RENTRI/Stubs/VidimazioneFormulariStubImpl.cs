namespace GKit.RENTRI.Stubs.VidimazioneFormulari;

public partial class VidimazioneFormulariStub : BaseClient
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        OnPrepareRequest(client, request, url);
    }

    partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        OnProcessResponse(client, response);
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
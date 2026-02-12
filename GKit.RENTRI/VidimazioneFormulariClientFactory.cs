namespace GKit.RENTRI;

public class VidimazioneFormulariClientFactory(ClientOptions options)
{
    public VidimazioneFormulariClient CreateClient()
    {
        return new VidimazioneFormulariClient(new HttpClient(), options);
    }
}
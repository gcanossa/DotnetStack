namespace GKit.RENTRI;

public class VidimazioneFormulariClientFactory(ClientOptions options, ApiStatusProvider apiStatusProvider)
    : BaseClientFactory<VidimazioneFormulariClient>(apiStatusProvider)
{
    protected override VidimazioneFormulariClient BuildClient()
    {
        return new VidimazioneFormulariClient(new HttpClient(), options);
    }
}
namespace GKit.RENTRI;

public class VidimazioneFormulariClientFactory(ApiStatusProvider apiStatusProvider)
    : BaseClientFactory<VidimazioneFormulariClient>(apiStatusProvider)
{
    protected override VidimazioneFormulariClient BuildClient(ClientOptions options)
    {
        return new VidimazioneFormulariClient(RentriHttpClientFactory.Create(), options);
    }
}
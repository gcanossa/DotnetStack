using GKit.RENTRI.Stubs.VidimazioneFormulari;

namespace GKit.RENTRI;

public class VidimazioneFormulariClient : VidimazioneFormulariStub
{
    public VidimazioneFormulariClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
    }
}
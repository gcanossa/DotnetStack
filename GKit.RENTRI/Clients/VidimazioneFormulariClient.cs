using GKit.RENTRI.Stubs.VidimazioneFormulari;

namespace GKit.RENTRI;

public class VidimazioneFormulariClient : VidimazioneFormulariStub
{
    public VidimazioneFormulariClient(HttpClient httpClient, ClientOptions options) : base(httpClient)
    {
        Options = options;
        if(Options is not null)
        {
            BaseUrl = BaseUrl.Replace("https://api.rentri.gov.it", Options.BaseUrl.TrimEnd('/'));
        }
    }
}
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using GKit.RENTRI;
using Xunit.Abstractions;

namespace Test.Repo.RENTRI;

public class CodificheClientTest
{
    private ITestOutputHelper _output;

    private ClientOptions _options;

    public CodificheClientTest(ITestOutputHelper output)
    {
        _output = output;

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("../../../RENTRI/test/certificate-data.json"));
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            "../../../RENTRI/test/certificate.p12",
            data!["password"],
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

        _options = new ClientOptions()
        {
            Audience = data.ContainsKey("audience") ? data["audience"] : "",
            BaseUrl = data.ContainsKey("baseUrl") ? data["baseUrl"] : "",
            Issuer = data.ContainsKey("issuer") ? data["issuer"] : "",
            Certificate = certificate
        };
    }

    [Fact]
    public async Task GetComuni()
    {
        var client = new CodificheClient(new HttpClient(), _options);

        var resp = await client.ComuniAsync("it");

        _output.WriteLine("{0}", resp.FirstOrDefault());
    }
}
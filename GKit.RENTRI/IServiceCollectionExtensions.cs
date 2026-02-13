using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.RENTRI;

public static class RentriExtensions
{
    private static void AddRentriClientFactories(IServiceCollection serviceCollection, ClientOptions clientOptions)
    {
        serviceCollection.AddSingleton(new AnagraficheClientFactory(clientOptions));
        serviceCollection.AddSingleton(new CaRentriClientFactory(clientOptions));
        serviceCollection.AddSingleton(new CodificheClientFactory(clientOptions));
        serviceCollection.AddSingleton(new DatiRegistriClientFactory(clientOptions));
        serviceCollection.AddSingleton(new FormulariClientFactory(clientOptions));
        serviceCollection.AddSingleton(new VidimazioneFormulariClientFactory(clientOptions));
    }

    public static IServiceCollection AddRentriProductionFactories(this IServiceCollection serviceCollection,
        string issuer, string certFilePath, string certPassword)
    {
        var clientOptions = new ClientOptions()
        {
            Issuer = issuer,
            Audience = "rentrigov.api",
            BaseUrl = "https://api.rentri.gov.it",
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certFilePath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet),
        };

        AddRentriClientFactories(serviceCollection, clientOptions);

        return serviceCollection;
    }


    public static IServiceCollection AddRentriDemoFactories(this IServiceCollection serviceCollection,
        string issuer, string certFilePath, string certPassword)
    {
        var clientOptions = new ClientOptions()
        {
            Issuer = issuer,
            Audience = "rentrigov.demo.api",
            BaseUrl = "https://demoapi.rentri.gov.it",
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certFilePath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet),
        };

        AddRentriClientFactories(serviceCollection, clientOptions);

        return serviceCollection;
    }
}
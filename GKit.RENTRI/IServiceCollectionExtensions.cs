using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.RENTRI;

public static class RentriExtensions
{
    public static IServiceCollection AddRentriServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ApiStatusProvider>();
        serviceCollection.AddHostedService<ApiStatusService>();

        serviceCollection.AddSingleton(provider => new AnagraficheClientFactory(provider.GetRequiredService<ApiStatusProvider>()));
        serviceCollection.AddSingleton(provider => new CaRentriClientFactory(provider.GetRequiredService<ApiStatusProvider>()));
        serviceCollection.AddSingleton(provider => new CodificheClientFactory(provider.GetRequiredService<ApiStatusProvider>()));
        serviceCollection.AddSingleton(provider => new DatiRegistriClientFactory(provider.GetRequiredService<ApiStatusProvider>()));
        serviceCollection.AddSingleton(provider => new FormulariClientFactory(provider.GetRequiredService<ApiStatusProvider>()));
        serviceCollection.AddSingleton(provider => new VidimazioneFormulariClientFactory(provider.GetRequiredService<ApiStatusProvider>()));

        return serviceCollection;
    }
}
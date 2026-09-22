using Microsoft.Extensions.DependencyInjection;

namespace GKit.RENTRI;

public static class RentriExtensions
{
    public static IServiceCollection AddRentriServices(
        this IServiceCollection serviceCollection,
        Action<RentriOptions>? configure = null)
    {
        serviceCollection.AddOptions<RentriOptions>()
            .BindConfiguration("GKit:Rentri")
            .Configure(options => configure?.Invoke(options));

        serviceCollection.AddRentriHttpClient();

        serviceCollection.AddSingleton<ApiStatusProvider>();
        serviceCollection.AddSingleton<ApiResultsCache>();
        serviceCollection.AddHostedService<ApiStatusService>();

        serviceCollection.AddSingleton<AnagraficheClientFactory>();
        serviceCollection.AddSingleton<CaRentriClientFactory>();
        serviceCollection.AddSingleton<CodificheClientFactory>();
        serviceCollection.AddSingleton<DatiRegistriClientFactory>();
        serviceCollection.AddSingleton<FormulariClientFactory>();
        serviceCollection.AddSingleton<VidimazioneFormulariClientFactory>();

        return serviceCollection;
    }
}

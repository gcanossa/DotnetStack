using Microsoft.Extensions.DependencyInjection;

namespace GKit.SmartCardHost;

public static class GKitSmartCardHostExtensions
{
    public static IServiceCollection AddGKitSmartCardHost(this IServiceCollection services)
    {
        services.AddSingleton<SmartCardStateBroker>();
        services.AddHostedService<SmartCardManager>();

        services.AddSignalR();

        return services;
    }

    public static HubEndpointConventionBuilder MapGKitSmartCardHost(this WebApplication builder)
    {
        return builder.MapHub<CardHub>("/card-reader");
    }
}
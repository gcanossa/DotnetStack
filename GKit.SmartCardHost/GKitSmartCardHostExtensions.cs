using Microsoft.Extensions.DependencyInjection;

namespace GKit.SmartCardHost;

public static class GKitSmartCardHostExtensions
{
    public static IServiceCollection AddGKitSmartCardHost(this IServiceCollection services)
    {
        services.AddSingleton<SmartCardState>();
        services.AddHostedService<SmartCardHostedService>();

        return services;
    }

    public static HubEndpointConventionBuilder MapGKitSmartCardHost(this WebApplication builder)
    {
        return builder.MapHub<CardHub>("/card-reader");
    }
}
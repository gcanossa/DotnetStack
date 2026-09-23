using Microsoft.Extensions.DependencyInjection;

namespace GKit.SmartCardHost;

public static class GKitSmartCardHostExtensions
{
    /// <summary>
    /// Hosts the smart card monitor as an external sidecar: state is broadcast to remote clients
    /// over SignalR (see SmartCardStateBroker). Call MapGKitSmartCardHost to expose the hub.
    /// </summary>
    public static IServiceCollection AddGKitSmartCardHost(this IServiceCollection services)
    {
        services.AddSingleton<SmartCardStateBroker>();
        services.AddSingleton<ISmartCardStateSink>(sp => sp.GetRequiredService<SmartCardStateBroker>());
        services.AddHostedService<SmartCardManager>();

        services.AddSignalR();

        return services;
    }

    /// <summary>
    /// Hosts the smart card monitor in-process: state is exposed locally via SmartCardState
    /// (Readers/LastAvailableCard* and its events), with no SignalR involved.
    /// </summary>
    public static IServiceCollection AddGKitSmartCardHostInProcess(this IServiceCollection services)
    {
        services.AddSingleton<SmartCardState>();
        services.AddSingleton<ISmartCardStateSink>(sp => sp.GetRequiredService<SmartCardState>());
        services.AddHostedService<SmartCardManager>();

        return services;
    }

    public static HubEndpointConventionBuilder MapGKitSmartCardHost(this WebApplication builder)
    {
        return builder.MapHub<CardHub>("/card-reader");
    }
}
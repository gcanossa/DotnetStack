using Microsoft.Extensions.DependencyInjection;

namespace GKit.SmartCardHost.Blazor;

public static class GKitSmartCardHostBlazorExtensions
{
    public static IServiceCollection AddGKitSmartCardHostService(this IServiceCollection ext)
    {
        // SmartCardHostService takes a SmartCardHostServiceHandle; without this registration
        // resolving the service threw "Unable to resolve service for type ...Handle".
        ext.AddScoped<SmartCardHostServiceHandle>();
        ext.AddScoped<SmartCardHostService>();

        return ext;
    }
}
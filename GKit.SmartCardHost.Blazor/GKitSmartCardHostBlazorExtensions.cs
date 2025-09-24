using Microsoft.Extensions.DependencyInjection;

namespace GKit.SmartCardHost.Blazor;

public static class GKitSmartCardHostBlazorExtensions
{
    public static IServiceCollection AddGKitSmartCardHostService(this IServiceCollection ext)
    {
        ext.AddScoped<SmartCardHostService>();

        return ext;
    }
}
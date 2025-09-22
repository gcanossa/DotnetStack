using Microsoft.Extensions.DependencyInjection;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace GKit.OpcUa;

public static class GKitOpcUaExtensions
{
    public static IServiceCollection AddOpcUaContextFactory<T>(
        this IServiceCollection services, 
        Action<ApplicationConfiguration> appOptions, 
        Action<IOpcUaContextOptions<T>> config)
        where T : OpcUaContext
    {
        
        services.AddSingleton<IOpcUaContextOptions<T>>(p =>
        {
            var options  = new OpcUaContextOptions<T>();
            config(options);
            var app = new ApplicationConfiguration();
            appOptions.Invoke(app);
            
            options.ApplicationConfiguration = app;
            return options;
        });

        services.AddScoped<IOpcUaContextFactory<T>, OpcUaContextFactory<T>>();
        
        return services;
    }
}
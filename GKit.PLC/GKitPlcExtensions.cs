using Microsoft.Extensions.DependencyInjection;

namespace GKit.PLC;

public static class GKitPlcExtensions
{
    public static IServiceCollection AddPlcContextFactory<T>(
        this IServiceCollection services, 
        Func<PlcContextOptionsBuilder<T>, IPlcContextOptionsSpecBuilder<T>> builder)
        where T : PlcContext
    {
        
        services.AddSingleton<IPlcContextOptions<T>>(provider =>
        {
            var optionsBuilder = new PlcContextOptionsBuilder<T>(new PlcContextOptions<T>(), provider);
            
            return builder.Invoke(optionsBuilder).BuildAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        });

        services.AddScoped<IPlcContextFactory<T>, PlcContextFactory<T>>();
        
        return services;
    }
}
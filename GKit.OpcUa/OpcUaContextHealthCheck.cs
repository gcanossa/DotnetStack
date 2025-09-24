using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GKit.OpcUa;

public static class OpcUaContextHealthCheckExtensions
{
    public static IHealthChecksBuilder AddOpcUaContextCheck<T>(this IHealthChecksBuilder ext, string name)
        where T : OpcUaContext
    {
        var builder = ext.AddCheck<OpcUaContextHealthCheck<T>>(name);
    
        return builder;
    }
}

public class OpcUaContextHealthCheck<T>(IOpcUaContextFactory<T> factory) : IHealthCheck
    where T : OpcUaContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var opcUaContext = await factory.CreateContextAsync(cancellationToken);
        
        return !opcUaContext.Connection.Connected ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();
    }
}
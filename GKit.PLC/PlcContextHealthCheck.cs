using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GKit.PLC;

public static class PlcContextHealthCheckExtensions
{
    public static IHealthChecksBuilder AddPlcContextCheck<T>(this IHealthChecksBuilder ext, string name)
        where T : PlcContext
    {
        var builder = ext.AddCheck<PlcContextHealthCheck<T>>(name);
    
        return builder;
    }
}

public class PlcContextHealthCheck<T>(IPlcContextFactory<T> factory) : IHealthCheck
    where T : PlcContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var plcContext = await factory.CreateContextAsync(cancellationToken);
        
        return !(plcContext.Connection?.IsConnected ?? false) ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();
    }
}
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
        try
        {
            using var opcUaContext = await factory.CreateContextAsync(cancellationToken);

            return opcUaContext.Connection.Connected
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("OPC UA session is not connected");
        }
        catch (Exception e)
        {
            // A probe must report, not throw: an unhandled exception here surfaces as an
            // opaque 500 from the health endpoint rather than an Unhealthy entry.
            return HealthCheckResult.Unhealthy("OPC UA connection failed", e);
        }
    }
}
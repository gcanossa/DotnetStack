using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GKit.SmartCardHost;

public static class SmartCardHostHealthCheckExtensions
{
    public static IHealthChecksBuilder AddSmartCardHostCheck(this IHealthChecksBuilder ext, string name)
    {
        var builder = ext.AddCheck<SmartCardHostHealthCheck>(name);

        return builder;
    }
}

public class SmartCardHostHealthCheck(SmartCardState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        var result = state.Readers.Length == 0 ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();

        return Task.FromResult(result);
    }
}
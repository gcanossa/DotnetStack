using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GKit.RENTRI;

public static class RentriHealthCheckExtensions
{
    public static IHealthChecksBuilder AddRentriCheck(this IHealthChecksBuilder ext, string name)
    {
        var builder = ext.AddCheck<RentriHealthCheck>(name);

        return builder;
    }
}

public class RentriHealthCheck(ApiStatusProvider statusProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        var result = statusProvider.Status != ApiStatus.Available ? HealthCheckResult.Unhealthy() : HealthCheckResult.Healthy();

        return Task.FromResult(result);
    }
}
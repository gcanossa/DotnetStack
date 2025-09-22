using Microsoft.Extensions.Logging;

namespace GKit.OpcUa;

public interface IOpcUaContextFactory<T> where T : OpcUaContext
{
    Task<T> CreateContextAsync(CancellationToken ct = default);
}

internal class OpcUaContextFactory<T>(IOpcUaContextOptions<T> options, ILogger<T> logger) : IOpcUaContextFactory<T>
    where T : OpcUaContext
{
    private IOpcUaContextOptions<T> Options { get; } = options;

    public async Task<T> CreateContextAsync(CancellationToken ct = default)
    {
        var context = (T)Activator.CreateInstance(typeof(T), Options)!;

        await context.Connection!.ConnectAsync(ct);
        
        return context;
    }
}
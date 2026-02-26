namespace GKit.OpcUa;

public interface IOpcUaContextFactory<T> where T : OpcUaContext
{
    Task<T> CreateContextAsync(CancellationToken ct = default);
}

internal class OpcUaContextFactory<T>(IOpcUaContextOptions<T> options) : IOpcUaContextFactory<T>
    where T : OpcUaContext
{
    private IOpcUaContextOptions<T> Options { get; } = options;

    public async Task<T> CreateContextAsync(CancellationToken ct = default)
    {
        var context = (T)Activator.CreateInstance(typeof(T), Options)!;

        await context.RenewConnection(ct).ConfigureAwait(false);

        return context;
    }
}
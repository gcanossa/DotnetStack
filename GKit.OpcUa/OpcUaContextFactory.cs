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

        // EnsureConnected, not RenewConnection. RenewConnection closes and disposes the pooled
        // session first, so every CreateContextAsync destroyed and rebuilt the shared session —
        // dropping subscriptions and churning sessions on the server on every job tick and
        // every health-check probe. Reserve RenewConnection for explicit recovery.
        await context.EnsureConnected(ct).ConfigureAwait(false);

        return context;
    }
}
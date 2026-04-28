namespace GKit.PLC;

public interface IPlcContextFactory<T> where T : PlcContext
{
    Task<T> CreateContextAsync(CancellationToken ct = default);
}

internal class PlcContextFactory<T>(IPlcContextOptions<T> options) : IPlcContextFactory<T>
    where T : PlcContext
{
    private IPlcContextOptions<T> Options { get; } = options;

    public async Task<T> CreateContextAsync(CancellationToken ct = default)
    {
        var context = (T)Activator.CreateInstance(typeof(T), Options)!;

        await context.RenewConnection(ct).ConfigureAwait(false);

        return context;
    }
}
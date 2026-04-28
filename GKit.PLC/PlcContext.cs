using System.Reflection;
using System.Runtime.CompilerServices;
using S7.Net;

namespace GKit.PLC;

public abstract partial class PlcContext : IDisposable, IAsyncDisposable
{
    protected IPlcContextOptions Options { get; init; }

    public Plc? Connection { get; protected set; }

    public PlcContext(IPlcContextOptions options)
    {
        Options = options;

        EntityModels = InitializeModels();
    }

    private Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> InitializeModels()
    {
        var modelBuilder = new ModelBuilder();
        OnModelCreating(modelBuilder);
        return modelBuilder.EntityModels;
    }

    internal Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> EntityModels { get; init; }

    protected abstract void OnModelCreating(IModelBuilder modelBuilder);

    private bool _disposed;

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        EntityModels.Clear();

        await CloseConnectionAsync(CancellationToken.None);
        
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    public async Task EnsureConnected(CancellationToken ct = default)
    {
        if(Connection is not { IsConnected: true })
            await OpenConnectionAsync(ct).ConfigureAwait(false);
        
        if (!Connection!.IsConnected) throw new InvalidOperationException("Connection failed");
    }

    public async Task RenewConnection(CancellationToken ct = default)
    {
        try
        {
            await CloseConnectionAsync(ct);
        }
        catch
        {
            // ignored
        }

        await OpenConnectionAsync(ct);
    }

    public async Task OpenConnectionAsync(CancellationToken ct = default)
    {
        Connection = new Plc(Options.CpuType, Options.Address.ToString(), Options.Port, Options.Rack, Options.Slot);
        await Connection.OpenAsync(ct);
    }

    public async Task CloseConnectionAsync(CancellationToken ct = default)
    {
        if (Connection != null)
        {
            if(Connection.IsConnected)
                Connection.Close();
            Connection = null;
        }
    }

    protected async Task<T> GuardRequestAsync<T>(Func<Task<T>> request, CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        try
        {
            return await request().ConfigureAwait(false);
        }
        catch (PlcException e)
        {
            if (e.ErrorCode is ErrorCode.ConnectionError or ErrorCode.IPAddressNotAvailable)
            {
                await RenewConnection(ct);
            }

            throw;
        }
    }
}
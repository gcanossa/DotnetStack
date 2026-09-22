using System.Reflection;
using System.Runtime.CompilerServices;
using S7.Net;

namespace GKit.PLC;

public abstract partial class PlcContext : IDisposable, IAsyncDisposable
{
    protected IPlcContextOptions Options { get; init; }

    public Plc? Connection { get; protected set; }

    // EnsureConnected had no mutual exclusion: two concurrent reads both saw IsConnected false,
    // both assigned Connection, and one socket was leaked per race. S7-300/400 CPUs allow only
    // a handful of concurrent PG/OP connections, so leaks lock out the engineering station.
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public PlcContext(IPlcContextOptions options)
    {
        Options = options;

        EntityModels = InitializeModels();
    }

    private Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> InitializeModels()
    {
        var modelBuilder = new ModelBuilder();
        OnModelCreating(modelBuilder);
        modelBuilder.Validate();
        return modelBuilder.EntityModels;
    }

    internal Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> EntityModels { get; init; }

    protected abstract void OnModelCreating(IModelBuilder modelBuilder);

    private bool _disposed;

    public void Dispose()
    {
        // Was DisposeAsync().GetAwaiter().GetResult(): a deadlock shape under a synchronisation
        // context. Closing the socket is synchronous anyway.
        if (_disposed) return;

        EntityModels.Clear();
        CloseConnectionCore();
        _connectionLock.Dispose();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    public async Task EnsureConnected(CancellationToken ct = default)
    {
        if (Connection is { IsConnected: true }) return;

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Connection is { IsConnected: true }) return;

            await OpenConnectionCoreAsync(ct).ConfigureAwait(false);

            if (!Connection!.IsConnected) throw new InvalidOperationException("Connection failed");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task RenewConnection(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CloseConnectionCore();
            await OpenConnectionCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task OpenConnectionAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await OpenConnectionCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task OpenConnectionCoreAsync(CancellationToken ct)
    {
        // Close the previous socket first: assigning over a live Plc orphaned it.
        CloseConnectionCore();

        Connection = new Plc(Options.CpuType, Options.Address.ToString(), Options.Port, Options.Rack, Options.Slot);
        await Connection.OpenAsync(ct).ConfigureAwait(false);
    }

    public Task CloseConnectionAsync(CancellationToken ct = default)
    {
        CloseConnectionCore();

        return Task.CompletedTask;
    }

    private void CloseConnectionCore()
    {
        if (Connection is null) return;

        try
        {
            if (Connection.IsConnected) Connection.Close();
        }
        catch
        {
            // Closing a socket that is already gone must not mask the caller's intent.
        }

        Connection = null;
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
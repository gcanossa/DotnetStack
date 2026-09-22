using System.Reflection;
using System.Runtime.CompilerServices;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;

namespace GKit.OpcUa;

public abstract partial class OpcUaContext : IDisposable
{
    protected IOpcUaContextOptions Options { get; init; }

    public OpcUaConnection Connection => OpcUaConnectionPool.Connections.GetValueOrDefault(Options)
                                         ?? throw new InvalidOperationException("Connection not found");

    private OpcUaConnection GetOrCreateConnection() => OpcUaConnectionPool.GetOrCreate(Options);

    public OpcUaContext(IOpcUaContextOptions options)
    {
        Options = options;
        
        EntityModels = InitializeModels();
    }

    private Dictionary<Type,Dictionary<PropertyInfo,EntityPropertyDescriptor>> InitializeModels()
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
        if (_disposed) return;
        
        EntityModels.Clear();
        
        _disposed = true;
            
        GC.SuppressFinalize(this);
    }

    public async Task EnsureConnected(CancellationToken ct = default)
    {
        // GetOrAdd rather than the Connection property: the pool may legitimately be empty on
        // the first call, and ConnectAsync is a no-op when the session is already up.
        var connection = GetOrCreateConnection();

        var connected = await connection.ConnectAsync(ct).ConfigureAwait(false);
        if (!connected) throw new InvalidOperationException("Connection failed");
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
        // TryAdd previously dropped the losing instance on the floor undisposed whenever two
        // callers raced. GetOrAdd keeps exactly one and never constructs an orphan.
        var connection = GetOrCreateConnection();

        await connection.ConnectAsync(ct).ConfigureAwait(false);
    }
    
    public async Task CloseConnectionAsync(CancellationToken ct = default)
    {
        var connection = OpcUaConnectionPool.Remove(Options);

        if (connection != null)
        {
            await connection.DisconnectAsync(false, ct).ConfigureAwait(false);
            connection.Dispose();
        }
    }
    
    protected async Task<T> GuardRequestAsync<T>(Func<Task<T>> request, CancellationToken ct = default)
    {
        await EnsureConnected(ct).ConfigureAwait(false);

        try
        {
            return await request().ConfigureAwait(false);
        }
        catch (ServiceResultException e)
        {
            if (e.StatusCode is StatusCodes.BadNotConnected or StatusCodes.BadTooManySessions)
            {
                await RenewConnection(ct);
            }

            throw;
        }
    }
}
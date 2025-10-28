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

    public OpcUaContext(IOpcUaContextOptions options)
    {
        Options = options;
        
        var modelBuilder = new ModelBuilder();
        OnModelCreating(modelBuilder);
        EntityModels = modelBuilder.EntityModels;

        OpcUaConnectionPool.Connections.TryAdd(Options, new OpcUaConnection(options));
    }
    
    internal Dictionary<Type, Dictionary<PropertyInfo, EntityPropertyDescriptor>> EntityModels { get; init; }

    protected virtual void OnModelCreating(IModelBuilder modelBuilder)
    {
        
    }
    
    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        
        OpcUaConnectionPool.Connections.Remove(Options, out var connection);
            
        connection?.Dispose();
            
        _disposed = true;
            
        GC.SuppressFinalize(this);
    }

    protected async Task EnsureConnected(CancellationToken ct = default)
    {
        var connected = await Connection.ConnectAsync(ct).ConfigureAwait(false);
        if (!connected) throw new InvalidOperationException("Connection failed");
    }
    
    
}
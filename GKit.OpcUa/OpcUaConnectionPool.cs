using System.Collections.Concurrent;

namespace GKit.OpcUa;

/// <summary>
/// One OPC UA session per distinct options instance, shared by every context built from it.
/// <para>
/// Sessions are expensive and servers cap how many they will hold, so tearing one down and
/// rebuilding it per operation is the failure mode this pool exists to prevent.
/// </para>
/// </summary>
internal static class OpcUaConnectionPool
{
    public static ConcurrentDictionary<IOpcUaContextOptions, OpcUaConnection> Connections { get; } = new();

    /// <summary>
    /// Returns the pooled connection, creating one only if absent.
    /// <para>
    /// <c>TryAdd</c> was used previously: under a race the losing thread's freshly constructed
    /// <see cref="OpcUaConnection"/> was dropped on the floor undisposed, and its
    /// <c>ConnectAsync</c> was never called.
    /// </para>
    /// </summary>
    public static OpcUaConnection GetOrCreate(IOpcUaContextOptions options) =>
        Connections.GetOrAdd(options, static key => new OpcUaConnection(key));

    /// <summary>Removes and returns the pooled connection, if any.</summary>
    public static OpcUaConnection? Remove(IOpcUaContextOptions options)
    {
        Connections.Remove(options, out var connection);
        return connection;
    }
}

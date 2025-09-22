using System.Collections.Concurrent;

namespace GKit.OpcUa;

internal static class OpcUaConnectionPool
{
    public static ConcurrentDictionary<IOpcUaContextOptions, OpcUaConnection> Connections { get; } =  new ();
}
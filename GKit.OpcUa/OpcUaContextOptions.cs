using Opc.Ua;
using Opc.Ua.Client;

namespace GKit.OpcUa;

public interface IOpcUaContextOptions
{
    public string ServerUrl { get; set; }
    
    public ReverseConnectManager? ReverseConnectManager { get; }
    public CertificateValidator? CertificateValidator { get; }
    public IUserIdentity? UserIdentity { get; }
    public ApplicationConfiguration ApplicationConfiguration { get; }
    
    public bool AcceptUntrustedCertificates { get; }
    public TimeSpan KeepAliveInterval { get; }
    public TimeSpan ReconnectPeriod { get; }
    public TimeSpan ReconnectPeriodExponentialBackoff { get; }
    public TimeSpan SessionLifeTime { get; }
}

internal class OpcUaContextOptions : IOpcUaContextOptions
{
    public string ServerUrl { get; set; }
    public ReverseConnectManager? ReverseConnectManager { get; internal set; }
    public CertificateValidator? CertificateValidator { get; internal set; }
    public IUserIdentity? UserIdentity { get; internal set; }
    public ApplicationConfiguration ApplicationConfiguration { get; internal set; }
    
    public bool AcceptUntrustedCertificates { get; set; }
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromMilliseconds(5000);
    public TimeSpan ReconnectPeriod { get; set; } = TimeSpan.FromMilliseconds(1000);
    public TimeSpan ReconnectPeriodExponentialBackoff { get; set; } = TimeSpan.FromMilliseconds(15000);
    public TimeSpan SessionLifeTime { get; set; } = TimeSpan.FromMilliseconds(60 * 1000);
}

public interface IOpcUaContextOptions<T> : IOpcUaContextOptions where T : OpcUaContext
{
    
}

internal class OpcUaContextOptions<T> : OpcUaContextOptions, IOpcUaContextOptions<T> where T : OpcUaContext
{
}
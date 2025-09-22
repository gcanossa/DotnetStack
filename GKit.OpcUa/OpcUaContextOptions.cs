using Opc.Ua;
using Opc.Ua.Client;

namespace GKit.OpcUa;

public interface IOpcUaContextOptions
{
    public string ServerUrl { get; set; }
    public Func<ReverseConnectManager>? ReverseConnectManager { get; set; }
    public Func<CertificateValidator>? CertificateValidator { get; set; }
    public Func<IUserIdentity>? UserIdentity { get; set; }
    public ApplicationConfiguration ApplicationConfiguration { get; set; }
    public bool AcceptUntrustedCertificates { get; set; }
    
    public int KeepAliveInterval { get; set; }

    public int ReconnectPeriod { get; set; }

    public int ReconnectPeriodExponentialBackoff { get; set; }

    public uint SessionLifeTime { get; set; }
}

internal class OpcUaContextOptions : IOpcUaContextOptions
{
    public string ServerUrl { get; set; } = null!;
    
    public Func<ReverseConnectManager>? ReverseConnectManager { get; set; }
    public Func<CertificateValidator>? CertificateValidator { get; set; }
    public Func<IUserIdentity>? UserIdentity { get; set; }
    public ApplicationConfiguration ApplicationConfiguration { get; set; }
    public bool AcceptUntrustedCertificates { get; set; }
    
    public int KeepAliveInterval { get; set; } = 5000;

    public int ReconnectPeriod { get; set; } = 1000;

    public int ReconnectPeriodExponentialBackoff { get; set; } = 15000;

    public uint SessionLifeTime { get; set; } = 60 * 1000;
}

public interface IOpcUaContextOptions<T> : IOpcUaContextOptions where T : OpcUaContext
{
    
}

internal class OpcUaContextOptions<T> : OpcUaContextOptions, IOpcUaContextOptions<T> where T : OpcUaContext
{
    
}
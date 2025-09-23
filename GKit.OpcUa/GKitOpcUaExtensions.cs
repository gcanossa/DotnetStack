using Microsoft.Extensions.DependencyInjection;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace GKit.OpcUa;

public static class GKitOpcUaExtensions
{
    public static IServiceCollection AddOpcUaContextFactory<T>(
        this IServiceCollection services, 
        Func<OpcUaContextOptionsBuilder<T>, IOpcUaContextOptionsSpecBuilder<T>> builder)
        where T : OpcUaContext
    {
        
        services.AddSingleton<IOpcUaContextOptions<T>>(provider =>
        {
            var optionsBuilder = new OpcUaContextOptionsBuilder<T>(new OpcUaContextOptions<T>(), provider);
            
            return builder.Invoke(optionsBuilder).BuildAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        });

        services.AddScoped<IOpcUaContextFactory<T>, OpcUaContextFactory<T>>();
        
        return services;
    }

    public static IApplicationConfigurationBuilderClientOptions WithDefaultWellKnownDiscoveryUrls(
        this IApplicationConfigurationBuilderClientOptions builder)
    {
        return builder.AddWellKnownDiscoveryUrls("opc.tcp://{0}:4840")
            .AddWellKnownDiscoveryUrls("http://{0}:52601/UADiscovery")
            .AddWellKnownDiscoveryUrls("http://{0}/UADiscovery/Default.svc");
    }
    
    public static IApplicationConfigurationBuilderSecurityOptions WithDynamicSelfSignedApplicationCertificate(
        this IApplicationConfigurationBuilderClientOptions builder, string applicationUri, string applicationName, string subjectName, params string[] subjectAlternativeNames)
    {
        return builder.AddSecurityConfiguration(new CertificateIdentifierCollection()
        {
            new CertificateIdentifier(
                CertificateFactory.CreateCertificate(
                    applicationUri,
                    applicationName,
                    subjectName,
                    subjectAlternativeNames).CreateForRSA(), CertificateValidationOptions.Default)
        });
    }

    public static string DefaultApplicationName<T>(this OpcUaContextOptionsBuilder<T> builder) where T : OpcUaContext
    {
        return typeof(T).FullName;
    }
    
    public static string DefaultApplicationUri<T>(this OpcUaContextOptionsBuilder<T> builder) where T : OpcUaContext
    {
        return $"urn:gkit:opcua:{typeof(T).FullName}";
    }
    
    public static string DefaultProductUri<T>(this OpcUaContextOptionsBuilder<T> builder) where T : OpcUaContext
    {
        return $"uri:opcfoundation.org:{typeof(T).FullName}";
    }
}
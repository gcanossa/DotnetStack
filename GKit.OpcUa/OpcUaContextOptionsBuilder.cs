using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace GKit.OpcUa;

public interface IOpcUaContextOptionsSpecBuilder<T> where T : OpcUaContext
{
    public IOpcUaContextOptionsSpecBuilder<T> WithReverseConnectManager(
        Func<IServiceProvider, Task<ReverseConnectManager>> provider);

    public IOpcUaContextOptionsSpecBuilder<T> WithCertificateValidator(
        Func<IServiceProvider, Task<CertificateValidator>> provider);

    public IOpcUaContextOptionsSpecBuilder<T> WithUserIdentity(Func<IServiceProvider, Task<IUserIdentity>> provider);

    public IOpcUaContextOptionsSpecBuilder<T> WithTelemetryContext(Func<IServiceProvider, Task<ITelemetryContext>> provider);

    public IOpcUaContextOptionsSpecBuilder<T> AcceptUntrustedCertificates(bool accept = true);

    public IOpcUaContextOptionsSpecBuilder<T> WithKeepAliveInterval(TimeSpan value);

    public IOpcUaContextOptionsSpecBuilder<T> WithReconnectPeriod(TimeSpan value);

    public IOpcUaContextOptionsSpecBuilder<T> WithReconnectPeriodExponentialBackoff(TimeSpan value);

    public IOpcUaContextOptionsSpecBuilder<T> WithSessionLifeTime(TimeSpan value);

    public Task<IOpcUaContextOptions<T>> BuildAsync();
}

internal class OpcUaContextOptionsSpecBuilder<T>(OpcUaContextOptionsBuilder<T> builder) : IOpcUaContextOptionsSpecBuilder<T> where T : OpcUaContext
{
    
    public IOpcUaContextOptionsSpecBuilder<T> WithReverseConnectManager(Func<IServiceProvider, Task<ReverseConnectManager>> provider)
    {
        builder.ReverseConnectManagerFactory = provider;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithCertificateValidator(Func<IServiceProvider, Task<CertificateValidator>> provider)
    {
        builder.CertificateValidatorFactory = provider;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithUserIdentity(Func<IServiceProvider, Task<IUserIdentity>> provider)
    {
        builder.UserIdentityFactory = provider;
        return this;
    }

    public IOpcUaContextOptionsSpecBuilder<T> WithTelemetryContext(Func<IServiceProvider, Task<ITelemetryContext>> provider)
    {
        builder.TelemetryContextFactory = provider;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> AcceptUntrustedCertificates(bool accept = true)
    {
        builder.Options.AcceptUntrustedCertificates = accept;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithKeepAliveInterval(TimeSpan value)
    {
        builder.Options.KeepAliveInterval = value;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithReconnectPeriod(TimeSpan value)
    {
        builder.Options.ReconnectPeriod = value;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithReconnectPeriodExponentialBackoff(TimeSpan value)
    {
        builder.Options.ReconnectPeriodExponentialBackoff = value;
        return this;
    }
    
    public IOpcUaContextOptionsSpecBuilder<T> WithSessionLifeTime(TimeSpan value)
    {
        builder.Options.SessionLifeTime = value;
        return this;
    }

    public async Task<IOpcUaContextOptions<T>> BuildAsync()
    {
        var appConfigurationBuilder = await builder.ApplicationConfigurationFactory!.Invoke(builder.ClientApplicationBuilder!, builder.ServiceProvider);
        
        builder.Options.ApplicationConfiguration = await appConfigurationBuilder.CreateAsync();
        
        builder.Options.ReverseConnectManager = builder.ReverseConnectManagerFactory is not null ? 
            await builder.ReverseConnectManagerFactory!.Invoke(builder.ServiceProvider) : null;
        
        builder.Options.ReverseConnectManager?.StartService(builder.Options.ApplicationConfiguration);
        
        builder.Options.CertificateValidator = builder.CertificateValidatorFactory is not null ? 
            await builder.CertificateValidatorFactory!.Invoke(builder.ServiceProvider) : null;
        
        builder.Options.UserIdentity = builder.UserIdentityFactory is not null ? 
            await builder.UserIdentityFactory!.Invoke(builder.ServiceProvider) : null;

        builder.Options.TelemetryContext = builder.TelemetryContextFactory is not null ?
            await builder.TelemetryContextFactory!.Invoke(builder.ServiceProvider) : DefaultTelemetry.Create(b => b.AddConsole());;
        
        return builder.Options;
    }
}

public sealed class OpcUaContextOptionsBuilder<T> where T : OpcUaContext
{
    internal OpcUaContextOptions<T> Options { get; }
    internal IServiceProvider ServiceProvider { get; }
    internal IApplicationConfigurationBuilderClientSelected? ClientApplicationBuilder { get; set; }
    
    internal Func<IApplicationConfigurationBuilderClientSelected, IServiceProvider, Task<IApplicationConfigurationBuilderCreate>>? ApplicationConfigurationFactory { get; set; }
    internal Func<IServiceProvider, Task<ReverseConnectManager>>? ReverseConnectManagerFactory { get; set; }
    internal Func<IServiceProvider, Task<CertificateValidator>>? CertificateValidatorFactory { get; set; }
    internal Func<IServiceProvider, Task<IUserIdentity>>? UserIdentityFactory { get; set; }
    internal Func<IServiceProvider, Task<ITelemetryContext>>? TelemetryContextFactory { get; set; }
    
    internal OpcUaContextOptionsBuilder(OpcUaContextOptions<T> options, IServiceProvider provider)
    {
        Options = options;
        ServiceProvider = provider;
    }

    public IOpcUaContextOptionsSpecBuilder<T> WithApplication(
        string serverUrl,
        Func<IApplicationConfigurationBuilderClientSelected, IServiceProvider, Task<IApplicationConfigurationBuilderCreate>> factory,
        string? applicationName = null, string? applicationUri = null, string? productUri = null)
    {
        Options.ServerUrl = serverUrl;
        
        ClientApplicationBuilder = new ApplicationInstance()
        {
            ApplicationType = ApplicationType.Client,
            ApplicationName = applicationName ?? this.DefaultApplicationName(),
            ApplicationConfiguration = new ApplicationConfiguration()
        }.Build(applicationUri ?? this.DefaultApplicationUri(),
            productUri ?? this.DefaultProductUri()).AsClient();
        
        ApplicationConfigurationFactory = factory;
        
        return new OpcUaContextOptionsSpecBuilder<T>(this);
    }
}

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmtpServer;

namespace GKit.SmtpHost;

public class SmtpServerFactory
{
    private readonly ILogger<SmtpServerFactory> _logger;
    private readonly IServiceProvider _provider;

    public SmtpServerFactory(
        ILogger<SmtpServerFactory> logger,
        IServiceProvider provider)
    {
        _logger = logger;
        _provider = provider;
    }

    public SmtpServer.SmtpServer Create(SmtpHostOptions options)
    {
        var smtpOptions = new SmtpServerOptionsBuilder()
            .ServerName(nameof(SmtpHost))
            .Endpoint(builder => {
                builder
                    .AuthenticationRequired(true)
                    .Port(options.Port);

                if(options.CertificatePem is not null && options.CertificateKeyPem is not null)
                {
                    builder
                        .AllowUnsecureAuthentication(false)
                        .Certificate(X509Certificate2.CreateFromPem(options.CertificatePem, options.CertificateKeyPem));
                }
                else
                {
                    builder
                        .AllowUnsecureAuthentication(true);
                }
            })
            .Build();

        var server = new SmtpServer.SmtpServer(smtpOptions, _provider.GetRequiredService<IServiceProvider>());

        _logger.LogInformation("SmtpServer ready to listen on: 0.0.0.0:{Port}", options.Port);

        return server;
    }
}
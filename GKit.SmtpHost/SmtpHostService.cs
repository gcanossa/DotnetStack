using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GKit.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    public class SmtpHostService : BackgroundService
    {
        private readonly SmtpServerFactory _smtpServerFactory;
        private readonly SettingsManager<SmtpHostOptions> _optionsManager;
        private IServiceProvider Provider { get; init; }
        private ISmtpHostBroker Broker { get; init; }
        private readonly ILogger<SmtpHostService> _logger;
        private SmtpServer.SmtpServer _server;

        public SmtpHostService(
            SmtpServerFactory smtpServerFactory,
            SettingsManager<SmtpHostOptions> optionsManager,
            IServiceProvider provider, 
            ISmtpHostBroker broker,
            ILogger<SmtpHostService> logger)
        {
            _smtpServerFactory = smtpServerFactory;
            _optionsManager = optionsManager;

            Provider = provider;
            Broker = broker;

            _logger = logger;

            _optionsManager.OptionsChanged += OnOptionsChanged;
        }

        public override void Dispose()
        {
            _optionsManager.OptionsChanged -= OnOptionsChanged;

            base.Dispose();
        }

        private async Task OnOptionsChanged(SmtpHostOptions options)
        {
            _server.Shutdown();
            await _server.ShutdownTask.ConfigureAwait(false);       
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processingTask = ProcessMessagesAsync(stoppingToken);
            var serverTask = Task.CompletedTask;
            
            while(!stoppingToken.IsCancellationRequested)
            {
                _server = _smtpServerFactory.Create(await _optionsManager.GetOptionsAsync());

                serverTask = _server.StartAsync(stoppingToken);
                await serverTask;
            }

            await Task.WhenAll(serverTask, processingTask);
        }

        private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
        {
            await foreach((MimeMessage Message, ISessionContext Context) item in Broker.ProcessAllAsync(stoppingToken))
            {
                var handled = false;

                using var scope = Provider.CreateAsyncScope();
                var provider = scope.ServiceProvider;

                using var processingScope = _logger.BeginScope(new {OperationId = Guid.NewGuid()});
                
                var handlers = provider.GetServices<IMessageHandler>();
                var deadLetterHandler = provider.GetRequiredService<IDeadLetterMessageHandler>();
                
                _logger.LogInformation("Processing message from {User}: ({Subject}) {From} -> {To}", 
                    item.Context.Authentication.User, item.Message.Subject, item.Message.From, item.Message.To);

                try
                {
                    foreach(var handler in handlers)
                    {
                        handled = await handler.Handle(provider, item.Message, item.Context);
                        if(handled)
                            break;
                    }

                    if(!handled)
                    {
                        _logger.LogWarning("Sending to dead letter");
                        await deadLetterHandler.Handle(provider, item.Message, item.Context);
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError(e, "Unable to process message");
                }
            }
        }
    }
}
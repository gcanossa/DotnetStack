using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using GKit.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GKit.TelegramHost
{
    internal class TelegramHostService : BackgroundService
    {
        private readonly TelegramConnectionFactory _connectionFactory;
        private readonly SettingsManager<TelegramHostOptions> _optionsManager;
        private readonly IServiceProvider _provider;
        private readonly ITelegramHostBroker _broker;

        private readonly ILogger<TelegramHostService> _logger;
        private TelegramConnection? _connection;

        public TelegramHostService(
            TelegramConnectionFactory connectionFactory,
            SettingsManager<TelegramHostOptions> optionsManager,
            IServiceProvider provider,
            ITelegramHostBroker broker, 
            ILogger<TelegramHostService> logger)
        {
            _connectionFactory = connectionFactory;
            _optionsManager = optionsManager;
            _provider = provider;
            _broker = broker;

            _logger = logger;

            _optionsManager.OptionsChanged += OnOptionsChanged;
        }

        public override void Dispose()
        {
            _optionsManager.OptionsChanged -= OnOptionsChanged;

            base.Dispose();
        }

        private Task OnOptionsChanged(TelegramHostOptions options)
        {
            _connection?.Stop();
            _connection = null;
            
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //TODO: reenable TL.RpcException: 400 PHONE_NUMBER_BANNED
            // var processingTask = ProcessMessagesAsync(stoppingToken);
            // var serverTask = Task.CompletedTask;

            // while(!stoppingToken.IsCancellationRequested)
            // {
            //     _connection = _connectionFactory.Create(await _optionsManager.GetOptionsAsync());

            //     serverTask = _connection.StartAsync(stoppingToken);
            //     await serverTask;
            // }

            // await Task.WhenAll(serverTask, processingTask);
        }

        private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
        {
            var jsonOptions = new JsonSerializerOptions() { IncludeFields = true };
            
            await foreach(TL.IObject item in _broker.ProcessAllAsync(stoppingToken))
            {
                var handled = false;

                using var scope = _provider.CreateAsyncScope();
                var provider = scope.ServiceProvider;

                using var processingScope = _logger.BeginScope(new {OperationId = Guid.NewGuid()});
                
                var handlers = provider.GetServices<IRequestHandler>();
                var deadLetterHandler = provider.GetRequiredService<IDeadLetterRequestHandler>();
                
                _logger.LogInformation("Processing message type {Type} => {Json}", 
                    item.GetType().Name, JsonSerializer.Serialize(item, jsonOptions));

                try
                {
                    foreach(var handler in handlers)
                    {
                        handled = await handler.Handle(provider, item);
                        if(handled)
                            break;
                    }

                    if(!handled)
                    {
                        _logger.LogWarning("Sending to dead letter");
                        await deadLetterHandler.Handle(provider, item);
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError(e, "Unable to process request");
                }
            }
        }
    }
}
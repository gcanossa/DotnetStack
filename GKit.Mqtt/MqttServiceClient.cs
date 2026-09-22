using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace GKit.Mqtt;

//TODO: test and fix, due to the lack of a real ManagedMqttClient
public class MqttServiceClient<T> : IHostedService where T : MqttControllerBase
{
    private readonly MqttClientFactory _factory;
    private readonly MqttServiceClientOptions<T> _options;
    private readonly IMqttClient _client;
    private readonly IServiceProvider _provider;

    private readonly MqttTopicRouter _router;

    private readonly ILogger<MqttServiceClient<T>> _logger;

    public MqttServiceClient(
        MqttServiceClientOptions<T> options,
        IServiceProvider provider,
        ILogger<MqttServiceClient<T>> logger)
    {
        _logger = logger;
        _provider = provider;
        _options = options;
        _factory = new MqttClientFactory();
        _client = _factory.CreateMqttClient();

        _router = new MqttTopicRouter(typeof(T));
    }

    protected async Task HandleApplicationMessage(MqttApplicationMessageReceivedEventArgs args)
    {
        using var scope = _provider.CreateAsyncScope();

        var controller = scope.ServiceProvider.GetRequiredService<T>();

        using var loggerScope = _logger.BeginScope(
            "{ClientId} => {Topic}", args.ClientId, args.ApplicationMessage.Topic);

        var topicHandlers = _router.Resolve(args.ApplicationMessage.Topic);

        if (topicHandlers.Count == 0)
        {
            _logger.LogWarning("No handler registered for topic.");
        }
        else
        {
            foreach (var handler in topicHandlers)
            {
                try
                {
                    _logger.LogDebug("Processing started");

                    var t = handler.Invoke(controller, [args]) as Task;
                    await (t ?? Task.CompletedTask);

                    _logger.LogDebug("Processing success");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error processing message");
                }
                finally
                {
                    _logger.LogDebug("Processing complete");
                }
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithCredentials(_options.UserName, _options.Password)
            .WithTcpServer(_options.Host, _options.Port)
            .Build();
        _client.ApplicationMessageReceivedAsync += HandleApplicationMessage;

        await _client.ConnectAsync(clientOptions, cancellationToken);

        var subscribeOptions = _factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f =>
            {
                foreach (var topic in _router.TopicFilters)
                    f.WithTopic(topic);
            }).Build();

        await _client.SubscribeAsync(subscribeOptions, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.DisconnectAsync(cancellationToken: cancellationToken);

        _client.ApplicationMessageReceivedAsync -= HandleApplicationMessage;

        _client.Dispose();
    }
}
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

    private readonly Dictionary<string, MethodInfo[]> _handlers;

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

        _handlers = typeof(T).GetMethods()
            .Select(p => new
            {
                topics = p.GetCustomAttributes(typeof(MqttTopicAttribute), false)
                    .Cast<MqttTopicAttribute>()
                    .Select(t => t.Topic),
                method = p
            })
            .SelectMany(p => p.topics.Select(t => new { topic = t, p.method }))
            .GroupBy(kv => kv.topic, kv => kv.method)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var m in _handlers.Values.SelectMany(p => p))
        {
            if (!m.ReturnType.IsAssignableTo(typeof(Task)))
                throw new ArgumentException($"Mqtt topic handler must return a Task object");

            if (!(m.GetParameters().FirstOrDefault()?.ParameterType ?? typeof(object))
                .IsAssignableTo(typeof(MqttApplicationMessageReceivedEventArgs)))
                throw new ArgumentException(
                    $"Mqtt topic handler must have the first parameter of type MqttApplicationMessageReceivedEventArgs");
        }
    }

    protected async Task HandleApplicationMessage(MqttApplicationMessageReceivedEventArgs args)
    {
        using var scope = _provider.CreateAsyncScope();

        var controller = scope.ServiceProvider.GetRequiredService<T>();

        using var loggerScope = _logger.BeginScope(
            "{ClientId} => {Topic}", args.ClientId, args.ApplicationMessage.Topic);

        if (!_handlers.TryGetValue(args.ApplicationMessage.Topic, out var topicHandlers))
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
                foreach (var topic in _handlers.Keys)
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
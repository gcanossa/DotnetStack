using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Server;

namespace GKit.Mqtt;

public interface IConnectionValidator
{
    Task ValidateAsync(ValidatingConnectionEventArgs args, MqttServer server);
}

public interface ISubscriptionInterceptor
{
    Task HandleAsync(InterceptingSubscriptionEventArgs args, MqttServer server);
}

public interface IPublishInterceptor
{
    Task HandleAsync(InterceptingPublishEventArgs args, MqttServer server);
}

public class MqttServerEventsHandler : IDisposable
{
    protected readonly MqttServer _server;
    protected readonly IServiceProvider _provider;

    public MqttServerEventsHandler(MqttServer server, IServiceProvider provider)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        _server.ValidatingConnectionAsync += OnConnectionValidation;
        _server.InterceptingSubscriptionAsync += OnInterceptingSubscription;
        _server.InterceptingPublishAsync += OnInterceptingPublish;
    }

    protected async Task OnConnectionValidation(ValidatingConnectionEventArgs args)
    {
        using var scope = _provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IConnectionValidator>();
        foreach (var h in handlers)
        {
            await h.ValidateAsync(args, _server);
        }
    }

    protected async Task OnInterceptingSubscription(InterceptingSubscriptionEventArgs args)
    {
        using var scope = _provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<ISubscriptionInterceptor>();
        foreach (var h in handlers)
        {
            await h.HandleAsync(args, _server);
        }
    }

    protected async Task OnInterceptingPublish(InterceptingPublishEventArgs args)
    {
        using var scope = _provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IPublishInterceptor>();
        foreach (var h in handlers)
        {
            await h.HandleAsync(args, _server);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _server.ValidatingConnectionAsync -= OnConnectionValidation;
        _server.InterceptingSubscriptionAsync -= OnInterceptingSubscription;
        _server.InterceptingPublishAsync -= OnInterceptingPublish;
    }
}
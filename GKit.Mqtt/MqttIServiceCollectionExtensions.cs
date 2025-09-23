using Microsoft.Extensions.DependencyInjection;

namespace GKit.Mqtt;

public static class MqttIServiceCollectionExtensions
{
    public static IServiceCollection AddMqttClient<C>(
        this IServiceCollection ext, Action<MqttServiceClientOptions<C>> config)
        where C : MqttControllerBase
    {
        _ = ext ?? throw new ArgumentNullException(nameof(ext));
        _ = config ?? throw new ArgumentNullException(nameof(config));

        var options = new MqttServiceClientOptions<C>();
        config.Invoke(options);

        ext.AddScoped<C>();

        ext.AddSingleton<MqttServiceClientOptions<C>>(options);
        ext.AddHostedService<MqttServiceClient<C>>();

        return ext;
    }
}
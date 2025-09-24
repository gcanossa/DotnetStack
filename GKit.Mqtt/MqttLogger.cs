using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics;
using MQTTnet.Diagnostics.Logger;

namespace GKit.Mqtt;

public class MqttLogger(ILogger<IMqttNetLogger> logger) : IMqttNetLogger
{
    public bool IsEnabled => true;

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters,
        Exception? exception)
    {
        var level = logLevel switch
        {
            MqttNetLogLevel.Info => LogLevel.Information,
            MqttNetLogLevel.Warning => LogLevel.Warning,
            MqttNetLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Debug
        };
        if (exception is not null)
        {
            logger.Log(level, exception, "[{Source}] {Message}", parameters);
        }
        else
        {
            logger.Log(level, "[{Source}] {Message}", parameters);
        }
    }
}
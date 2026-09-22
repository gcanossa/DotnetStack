using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics.Logger;

namespace GKit.Mqtt;

public class MqttLogger(ILogger<IMqttNetLogger> logger) : IMqttNetLogger
{
    /// <summary>
    /// Was hard-coded true, which bypassed the configured minimum level entirely.
    /// </summary>
    public bool IsEnabled => logger.IsEnabled(LogLevel.Debug);

    internal static LogLevel ToLogLevel(MqttNetLogLevel logLevel) => logLevel switch
    {
        MqttNetLogLevel.Info => LogLevel.Information,
        MqttNetLogLevel.Warning => LogLevel.Warning,
        MqttNetLogLevel.Error => LogLevel.Error,
        _ => LogLevel.Debug
    };

    /// <summary>
    /// MQTTnet hands over a message template plus its own parameter array. Formatting must
    /// happen here: passing <c>parameters</c> straight to <c>ILogger.Log</c> bound MQTTnet's
    /// arguments to <c>{Source}</c> and <c>{Message}</c>, so the structured log carried the
    /// wrong values — and <c>source</c> and <c>message</c> were never recorded at all.
    /// </summary>
    internal static string Format(string message, object[]? parameters)
    {
        if (parameters is null || parameters.Length == 0) return message;

        try
        {
            return string.Format(message, parameters);
        }
        catch (FormatException)
        {
            // A malformed template from a dependency must not take down the logging path.
            return message;
        }
    }

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters,
        Exception? exception)
    {
        var level = ToLogLevel(logLevel);

        if (!logger.IsEnabled(level)) return;

        logger.Log(level, exception, "[{Source}] {Message}", source, Format(message, parameters));
    }
}

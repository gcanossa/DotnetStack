using System.Reflection;
using MQTTnet;

namespace GKit.Mqtt;

/// <summary>
/// Maps a received topic onto the handler methods registered for it.
/// <para>
/// Dispatch used to be an exact dictionary lookup on the topic string, so a handler registered
/// for <c>sensors/+/temp</c> subscribed successfully and then never fired: the *received* topic
/// is <c>sensors/3/temp</c>. Every wildcard route silently dead-lettered.
/// </para>
/// <para>
/// Pure and injectable so routing can be verified without a broker.
/// </para>
/// </summary>
public class MqttTopicRouter
{
  private readonly Dictionary<string, MethodInfo[]> _routes;

  public MqttTopicRouter(Type controllerType)
  {
    ArgumentNullException.ThrowIfNull(controllerType);

    _routes = controllerType.GetMethods()
      .SelectMany(method => method.GetCustomAttributes<MqttTopicAttribute>()
        .Select(attribute => new { attribute.Topic, method }))
      .GroupBy(p => p.Topic, p => p.method)
      .ToDictionary(g => g.Key, g => g.ToArray());

    Validate();
  }

  /// <summary>The topic filters to subscribe to.</summary>
  public IReadOnlyCollection<string> TopicFilters => _routes.Keys;

  private void Validate()
  {
    foreach (var method in _routes.Values.SelectMany(p => p))
    {
      if (!method.ReturnType.IsAssignableTo(typeof(Task)))
        throw new ArgumentException(
          $"Mqtt topic handler {method.DeclaringType?.Name}.{method.Name} must return a Task.");

      var first = method.GetParameters().FirstOrDefault()?.ParameterType;
      if (first is null || !first.IsAssignableTo(typeof(MqttApplicationMessageReceivedEventArgs)))
        throw new ArgumentException(
          $"Mqtt topic handler {method.DeclaringType?.Name}.{method.Name} must take " +
          $"{nameof(MqttApplicationMessageReceivedEventArgs)} as its first parameter.");
    }
  }

  /// <summary>
  /// Every handler whose registered filter matches <paramref name="topic"/>, honouring the
  /// MQTT <c>+</c> (single level) and <c>#</c> (multi level) wildcards.
  /// </summary>
  public IReadOnlyList<MethodInfo> Resolve(string topic)
  {
    ArgumentNullException.ThrowIfNull(topic);

    return
    [
      .. _routes
        .Where(route => MqttTopicFilterComparer.Compare(topic, route.Key) == MqttTopicFilterCompareResult.IsMatch)
        .SelectMany(route => route.Value)
    ];
  }
}

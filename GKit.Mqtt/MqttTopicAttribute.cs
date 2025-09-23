namespace GKit.Mqtt;

[AttributeUsage(AttributeTargets.Method)]
public class MqttTopicAttribute(string topic) : Attribute
{
    public string Topic { get; } = topic;
}
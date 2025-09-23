using System.Net;

namespace GKit.Mqtt;

public class MqttWrappedPayload
{
    public string? Endpoint { get; set; }
    public string? ClientId { get; set; }
    public string? Payload { get; set; }
}
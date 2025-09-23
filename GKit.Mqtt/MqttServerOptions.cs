using System.Net;

namespace GKit.Mqtt;

public class EndpointOptions
{
    public IPAddress Address { get; set; } = IPAddress.Any;
    public int Port { get; set; }
}

public class MqttServerOptions
{
    public EndpointOptions? Mqtt { get; set; }
    public EndpointOptions? Mqtts { get; set; }
}
using System.Text.Json;
using MQTTnet;
using MQTTnet.Server;

namespace GKit.Mqtt;

public class MqttPayloadWrapper : IPublishInterceptor
{
    public async Task HandleAsync(InterceptingPublishEventArgs args, MqttServer server)
    {
        var endpoint = await server.GetClientEndpointAsync(args.ClientId);
        args.ApplicationMessage.PayloadSegment = System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new MqttWrappedPayload()
                {
                    Endpoint = endpoint is not null ? $"{endpoint.Address}:{endpoint.Port}" : null,
                    ClientId = args.ClientId,
                    Payload = args.ApplicationMessage.ConvertPayloadToString()
                },
                new JsonSerializerOptions() { WriteIndented = false }
            ));
    }
}
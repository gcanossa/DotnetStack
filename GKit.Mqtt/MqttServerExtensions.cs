using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace GKit.Mqtt;

public static class MqttServerExtensions
{
    public static async Task<IPEndPoint?> GetClientEndpointAsync(this MqttServer ext, string clientId)
    {
        _ = ext ?? throw new ArgumentNullException(nameof(ext));

        var endpoint = (await ext.GetClientsAsync()).FirstOrDefault(p => p.Id == clientId)?.Endpoint;

        return endpoint is not null ? IPEndPoint.Parse(endpoint) : null;
    }

    public static WebApplication MapMqtt(this WebApplication ext, Action<MqttServer> configure = null)
    {
        _ = ext ?? throw new ArgumentNullException(nameof(ext));

        ext.MapConnectionHandler<MqttConnectionHandler>(
            "/mqtt",
            httpConnectionDispatcherOptions => httpConnectionDispatcherOptions.WebSockets.SubProtocolSelector =
                protocolList => protocolList.FirstOrDefault() ?? string.Empty);

        ext.UseMqttServer(server =>
        {
            _ = new MqttServerEventsHandler(server, ext.Services);

            configure?.Invoke(server);
        });

        return ext;
    }
}
namespace GKit.Mqtt;

public class MqttServiceClientOptions<T> where T : MqttControllerBase
{
    public string? ClientId { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;

    public string? UserName { get; set; }
    public string? Password { get; set; }
}
namespace GKit.TelegramBotEndpoint;

public class TelegramBotInfo<T> where T : IUpdateHandler
{
    public string? Name { get; internal set; }
}
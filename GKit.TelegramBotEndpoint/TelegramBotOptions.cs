namespace GKit.TelegramBotEndpoint;

public class TelegramBotOptions<T> where T : IUpdateHandler
{
    public string? BotToken { get; set; }
    public bool AllowRegistration { get; set; } = true;
    public int LockoutErrorCount { get; set; } = 5;
}
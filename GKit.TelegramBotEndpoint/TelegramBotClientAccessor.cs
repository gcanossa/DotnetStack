using Telegram.Bot;

namespace GKit.TelegramBotEndpoint;

public class TelegramBotClientAccessor<T> where T : IUpdateHandler
{
    public ITelegramBotClient? Client { get; internal set; }
}
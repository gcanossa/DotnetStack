using Telegram.Bot;

namespace GKit.TelegramBotEndpoint;

public class TelegramBotClientAccessor
{
    public ITelegramBotClient Client { get; internal set; }
}
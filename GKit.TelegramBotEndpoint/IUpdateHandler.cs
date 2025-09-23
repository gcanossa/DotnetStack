using Telegram.Bot;
using Telegram.Bot.Types;
using GKit.TelegramBotEndpoint.Data;

namespace GKit.TelegramBotEndpoint;

public interface IUpdateHandler
{
    Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, TelegramUser user, long? chatId);
}
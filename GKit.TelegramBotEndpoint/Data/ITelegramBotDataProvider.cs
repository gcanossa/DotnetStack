namespace GKit.TelegramBotEndpoint.Data;

public interface ITelegramBotDataProvider
{
    event Func<Task> UsersChanged;

    Task<IEnumerable<TelegramUser>> GetUsers();

    Task<TelegramUser?> GetUserById(long id);
    
    Task CreateUser(TelegramUser user);
    Task UpdateUser(TelegramUser user);
    Task DeleteUser(long id);
}
using TL;

namespace GKit.TelegramHost;

public class TelegramContext
{
    private readonly TelegramContextProvider _contextProvider;
    internal TelegramContext(TelegramContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public async Task<IEnumerable<Model.Contact>> GetContactsAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        var client = await _contextProvider.GetSessionClientAsync();   
        var result = await client.Contacts_GetContacts();

        return result.users.Select(p => new Model.Contact(p.Key, p.Value.access_hash, p.Value.first_name));
    }

    public async Task SendMessageAsync(Model.Contact contact, string message, CancellationToken cancellationToken = default(CancellationToken))
    {
        var client = await _contextProvider.GetSessionClientAsync(); 
        var text = message;
        var entities = client.HtmlToEntities(ref text);
        await client.SendMessageAsync(new InputPeerUser(contact.Id, contact.AccessHash), text, entities: entities);
    }
}
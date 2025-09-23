namespace GKit.TelegramHost;

public class TelegramContextProvider
{
    private readonly object _sync = new object();

    private WTelegram.Client? _client = null;
    private TaskCompletionSource<WTelegram.Client> _clientReady = new TaskCompletionSource<WTelegram.Client>();

    internal void UpdateClient(WTelegram.Client client)
    {
        lock(_sync)
        {
            var shouldInvalidate = _client is not null;

            _client = client;

            if(shouldInvalidate)
            {
                InvalidateClient();
            }
        }
    }

    internal void ClientReady()
    {
        lock(_sync)
        {
            _clientReady.SetResult(_client!);
        }
    }

    internal void InvalidateClient()
    {
        lock(_sync)
        {
            var tmp = _clientReady;
            
            _clientReady = new TaskCompletionSource<WTelegram.Client>();
            
            if(!tmp.Task.IsCompleted)
            {
                tmp.SetCanceled();
            }
        }
    }

    internal Task<WTelegram.Client> GetSessionClientAsync()
    {
        lock(_sync)
        {
            return _clientReady.Task;
        }
    }

    public TelegramContext CreateContext()
    {
        return new TelegramContext(this);
    }
}
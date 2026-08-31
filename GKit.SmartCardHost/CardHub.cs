using Microsoft.AspNetCore.SignalR;

namespace GKit.SmartCardHost;

public interface ICardHub
{
    Task OnCardAvailable(string cardUid);
    Task OnReadersChanged(string[] readers);
}

public class CardHub : Hub<ICardHub>, IDisposable
{
    private readonly SmartCardState state;

    public CardHub(SmartCardState state)
    {
        this.state = state;

        state.ReadersChanged += OnReadersChanged;
        state.CardAvailable += OnCardAvailable;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        await Clients.Caller.OnReadersChanged(state.Readers);
    }

    protected void OnCardAvailable(string uid)
    {
        Clients.All.OnCardAvailable(uid)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    protected void OnReadersChanged(string[] readers)
    {
        Clients.All.OnReadersChanged(readers)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }
    
    protected override void Dispose(bool disposing)
    {
        state.ReadersChanged -= OnReadersChanged;
        state.CardAvailable -= OnCardAvailable;
    }
}
using Microsoft.AspNetCore.SignalR;

namespace GKit.SmartCardHost;

public class SmartCardStateBroker(IHubContext<CardHub, ICardHub> hubContext)
{
    public string[] Readers { get; protected set; } = [];

    public async Task OnCardAvailable(string uid)
    {
        await hubContext.Clients.All.OnCardAvailable(uid);
    }

    public async Task OnReadersChanged(string[] readers)
    {
        Readers = readers;

        await hubContext.Clients.All.OnReadersChanged(readers);
    }
}
using Microsoft.AspNetCore.SignalR;

namespace GKit.SmartCardHost;

public interface ICardHub
{
  Task OnCardAvailable(string cardUid);
  Task OnReadersChanged(string[] readers);
}

public class CardHub(SmartCardStateBroker broker) : Hub<ICardHub>
{
  public override async Task OnConnectedAsync()
  {
    await base.OnConnectedAsync();

    await Clients.Caller.OnReadersChanged(broker.Readers);
  }
}
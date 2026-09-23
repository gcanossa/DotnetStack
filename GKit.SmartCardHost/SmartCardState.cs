namespace GKit.SmartCardHost;

/// <summary>
/// In-process smart card state, registered by AddGKitSmartCardHostInProcess. Consumers read
/// Readers/LastAvailableCard* or subscribe to the events directly, in the same process as the
/// reader monitor; no SignalR involved. For the external-sidecar broadcast model, see
/// SmartCardStateBroker.
/// </summary>
public class SmartCardState : ISmartCardStateSink
{
    public string[] Readers { get; protected set; } = [];
    public string? LastAvailableCardId { get; protected set; }
    public DateTime? LastAvailableCardAt { get; set; }

    public event Action<string[]>? ReadersChanged;
    public event Action<string>? CardAvailable;

    Task ISmartCardStateSink.OnReadersChanged(string[] readers)
    {
        Readers = readers;

        ReadersChanged?.Invoke(readers);

        return Task.CompletedTask;
    }

    Task ISmartCardStateSink.OnCardAvailable(string cardId)
    {
        LastAvailableCardId = cardId;
        LastAvailableCardAt = DateTime.Now;

        CardAvailable?.Invoke(cardId);

        return Task.CompletedTask;
    }
}

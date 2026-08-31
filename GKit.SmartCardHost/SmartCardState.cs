namespace GKit.SmartCardHost;

public class SmartCardState
{
    public string[] Readers { get; protected set; } = [];
    public string? LastAvailableCardId { get; protected set; }
    public DateTime? LastAvailableCardAt { get; set; }
    
    public event Action<string[]>? ReadersChanged;
    public event Action<string>? CardAvailable;

    internal void OnReadersChanged(string[] readers)
    {
        Readers = readers;
        
        ReadersChanged?.Invoke(readers);
    }

    internal void OnCardAvailable(string cardId)
    {
        LastAvailableCardId = cardId;
        LastAvailableCardAt = DateTime.Now;
        
        CardAvailable?.Invoke(cardId);
    }
}
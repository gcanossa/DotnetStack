namespace GKit.SmartCardHost;

/// <summary>
/// Where SmartCardManager reports reader/card events. Implemented by SmartCardStateBroker
/// (external sidecar, broadcasts over SignalR) and SmartCardState (in-process, local events),
/// so AddGKitSmartCardHost/AddGKitSmartCardHostInProcess can select one without SmartCardManager
/// knowing which.
/// </summary>
public interface ISmartCardStateSink
{
    string[] Readers { get; }

    Task OnReadersChanged(string[] readers);
    Task OnCardAvailable(string uid);
}

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCSC;
using PCSC.Monitoring;

namespace GKit.SmartCardHost;

public class SmartCardManager(ISmartCardStateSink sink, ILogger<SmartCardManager> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var context = ContextFactory.Instance.Establish(SCardScope.System);

        var availableReaders = context.GetReaders();
        Array.Sort(availableReaders);

        await sink.OnReadersChanged(availableReaders);

        ISCardMonitor? monitor = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (context.CheckValidity() != SCardError.Success)
                context.Establish(SCardScope.System);

            var readers = context.GetReaders();
            Array.Sort(readers);

            if (!(monitor?.Monitoring ?? false) ||
                !readers.SequenceEqual(availableReaders, StringComparer.Ordinal))
            {
                availableReaders = readers;

                if (monitor?.Monitoring ?? false)
                {
                    monitor.Cancel();
                    monitor.Dispose();
                }

                if (availableReaders.Length > 0)
                {
                    monitor = CreateMonitor(context);

                    monitor.Start(availableReaders);
                    logger.LogInformation("Restarting reader monitor. Readers changed.");
                }

                await sink.OnReadersChanged(availableReaders);
            }

            await Task.Delay(5 * 1000, stoppingToken);
        }

        monitor?.Dispose();
    }

    protected ISCardMonitor CreateMonitor(ISCardContext context)
    {
        var monitor = MonitorFactory.Instance.Create(SCardScope.System);

        monitor.StatusChanged += (sender, evt) =>
        {
            if ((evt.NewState & SCRState.Unavailable) == SCRState.Unavailable)
                monitor.Cancel();
        };

        monitor.CardInserted += (sender, evt) =>
        {
            try
            {
                using var card = new SCardReader(context);
                var error = card.Connect(evt.ReaderName, SCardShareMode.Shared, SCardProtocol.Any);
                error.ThrowIfNotSuccess();

                // OnCard used to be `async void`: the handler returned at its first await and
                // disposed `card` while OnCard was still using it. Block here instead — the
                // PCSC monitor raises this on its own thread, not on a request thread.
                OnCard(card).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Error accessing the SmartCard");
            }
        };

        return monitor;
    }

    protected async Task OnCard(ISCardReader card)
    {
        try
        {
            var uid = Convert.ToHexString(card.GetUid());
            logger.LogInformation("Read Card uid: {UID}", uid);
            await sink.OnCardAvailable(uid);

            var error = card.Disconnect(SCardReaderDisposition.Leave);
            error.ThrowIfNotSuccess();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Error using the SmartCard");
        }
    }
}
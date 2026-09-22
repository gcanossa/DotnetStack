using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GKit.RENTRI;

public class ApiStatusService : BackgroundService
{
    private readonly ILogger<ApiStatusService> logger;

    private readonly AnagraficheClientFactory anagraficheClientFactory;
    private readonly CaRentriClientFactory caRentriClientFactory;
    private readonly CodificheClientFactory codificheClientFactory;
    private readonly DatiRegistriClientFactory datiRegistriClientFactory;
    private readonly FormulariClientFactory formulariClientFactory;
    private readonly VidimazioneFormulariClientFactory vidimazioneFormulariClientFactory;

    private readonly ApiStatusProvider apiStatusProvider;
    private readonly IOptions<RentriOptions> rentriOptions;

    public ApiStatusService(ILogger<ApiStatusService> logger,
        AnagraficheClientFactory anagraficheClientFactory,
        CaRentriClientFactory caRentriClientFactory,
        CodificheClientFactory codificheClientFactory,
        DatiRegistriClientFactory datiRegistriClientFactory,
        FormulariClientFactory formulariClientFactory,
        VidimazioneFormulariClientFactory vidimazioneFormulariClientFactory,
        ApiStatusProvider apiStatusProvider,
        IOptions<RentriOptions> rentriOptions)
    {
        this.logger = logger;
        this.rentriOptions = rentriOptions;
        this.anagraficheClientFactory = anagraficheClientFactory;
        this.caRentriClientFactory = caRentriClientFactory;
        this.codificheClientFactory = codificheClientFactory;
        this.datiRegistriClientFactory = datiRegistriClientFactory;
        this.formulariClientFactory = formulariClientFactory;
        this.vidimazioneFormulariClientFactory = vidimazioneFormulariClientFactory;

        this.apiStatusProvider = apiStatusProvider;
    }

    protected async Task<ApiStatus> GetStatus(Func<Task> checkStatus)
    {
        try
        {
            await checkStatus();

            return ApiStatus.Available;
        }
        catch (ApiException e)
        {
            // 401/403 used to be rethrown. Escaping ExecuteAsync trips the default
            // BackgroundServiceExceptionBehavior.StopHost, so an expired RENTRI certificate
            // took the whole application down instead of just marking the API unavailable.
            if (e.StatusCode is 401 or 403)
                logger.LogError(e, "RENTRI rejected the status probe with {StatusCode}", e.StatusCode);

            return ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "RENTRI status probe failed");

            return ApiStatus.Unavailable;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = rentriOptions.Value.StatusPollInterval;

        if (interval is null || interval <= TimeSpan.Zero)
        {
            logger.LogInformation("RENTRI status polling is disabled");
            return;
        }

        using var timer = new PeriodicTimer(interval.Value);

        do
        {
            try
            {
                await ProbeAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // Never let this escape: BackgroundService's default behaviour on an unhandled
                // exception is to stop the entire host.
                logger.LogWarning(e, "Unable to check RENTRI api status");
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ProbeAllAsync(CancellationToken stoppingToken)
    {
        using var anagraficheClient = anagraficheClientFactory.CreateAnonymousClient();
        using var caRentriClient = caRentriClientFactory.CreateAnonymousClient();
        using var codificheClient = codificheClientFactory.CreateAnonymousClient();
        using var datiRegistriClient = datiRegistriClientFactory.CreateAnonymousClient();
        using var formulariClient = formulariClientFactory.CreateAnonymousClient();
        using var vidimazioneFormulariClient = vidimazioneFormulariClientFactory.CreateAnonymousClient();

        using var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        source.CancelAfter(rentriOptions.Value.StatusProbeTimeout);

        apiStatusProvider.Set(RentriApi.Anagrafiche,
            await GetStatus(() => anagraficheClient.StatusAsync(source.Token)));
        apiStatusProvider.Set(RentriApi.CaRentri,
            await GetStatus(() => caRentriClient.Status2Async(source.Token)));
        apiStatusProvider.Set(RentriApi.Codifiche,
            await GetStatus(() => codificheClient.StatusAsync(source.Token)));
        apiStatusProvider.Set(RentriApi.DatiRegistri,
            await GetStatus(() => datiRegistriClient.Status2Async(source.Token)));
        apiStatusProvider.Set(RentriApi.Formulari,
            await GetStatus(() => formulariClient.Status2Async(source.Token)));
        apiStatusProvider.Set(RentriApi.VidimazioneFormulari,
            await GetStatus(() => vidimazioneFormulariClient.Status2Async(source.Token)));
    }
}

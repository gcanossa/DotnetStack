using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    public ApiStatusService(ILogger<ApiStatusService> logger,
        AnagraficheClientFactory anagraficheClientFactory,
        CaRentriClientFactory caRentriClientFactory,
        CodificheClientFactory codificheClientFactory,
        DatiRegistriClientFactory datiRegistriClientFactory,
        FormulariClientFactory formulariClientFactory,
        VidimazioneFormulariClientFactory vidimazioneFormulariClientFactory,
        ApiStatusProvider apiStatusProvider)
    {
        this.logger = logger;
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
            if (e.StatusCode is 401 or 403)
                throw;

            return ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var anagraficheClient = anagraficheClientFactory.CreateClient();
                using var caRentriClient = caRentriClientFactory.CreateClient();
                using var codificheClient = codificheClientFactory.CreateClient();
                using var datiRegistriClient = datiRegistriClientFactory.CreateClient();
                using var formulariClient = formulariClientFactory.CreateClient();
                using var vidimazioneFormulariClient = vidimazioneFormulariClientFactory.CreateClient();

                var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                apiStatusProvider.Anagrafiche = await GetStatus(() => anagraficheClient.StatusAsync(source.Token));
                apiStatusProvider.CaRentri = await GetStatus(() => caRentriClient.Status2Async(source.Token));
                apiStatusProvider.Codifiche = await GetStatus(() => codificheClient.StatusAsync(source.Token));
                apiStatusProvider.DatiRegistri = await GetStatus(() => datiRegistriClient.Status2Async(source.Token));
                apiStatusProvider.Formulari = await GetStatus(() => formulariClient.Status2Async(source.Token));
                apiStatusProvider.VidimazioneFormulari =
                    await GetStatus(() => vidimazioneFormulariClient.Status2Async(source.Token));

                await Task.Delay(5 * 60 * 1000, stoppingToken);
            }
            catch (ApiException e)
            {
                logger.LogError(e, "Authentication failed. Stopping service.");

                apiStatusProvider.Anagrafiche = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
                apiStatusProvider.CaRentri = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
                apiStatusProvider.Codifiche = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
                apiStatusProvider.DatiRegistri = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
                apiStatusProvider.Formulari = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);
                apiStatusProvider.VidimazioneFormulari = ApiStatusProvider.GetApiStatusFromHttpStatusCode(e.StatusCode);

                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unable to check RENTRI api status");
            }
        }
    }
}
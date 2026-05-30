using Microsoft.Extensions.Options;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Service;

public sealed class PosterUpdateWorker : BackgroundService
{
    private readonly ILogger<PosterUpdateWorker> logger;
    private readonly IOptions<PosterUpdateOptions> posterOptions;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ZidooPathMapper pathMapper;
    private readonly PosterStateStore stateStore;
    private readonly PosterPlanner planner;
    private readonly PosterRenderer renderer;
    private readonly PosterApplier applier;

    public PosterUpdateWorker(
        ILogger<PosterUpdateWorker> logger,
        IOptions<PosterUpdateOptions> posterOptions,
        IServiceScopeFactory scopeFactory,
        ZidooPathMapper pathMapper,
        PosterStateStore stateStore,
        PosterPlanner planner,
        PosterRenderer renderer,
        PosterApplier applier)
    {
        this.logger = logger;
        this.posterOptions = posterOptions;
        this.scopeFactory = scopeFactory;
        this.pathMapper = pathMapper;
        this.stateStore = stateStore;
        this.planner = planner;
        this.renderer = renderer;
        this.applier = applier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Poster update iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(posterOptions.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<ZidooApiClient>();
        var collections = await apiClient.GetCollectionListAsync(cancellationToken);
        foreach (var seriesItem in collections.Data.Where(TvLibraryDiscovery.IsTvSeries))
        {
            logger.LogInformation("Discovered TV series {SeriesId}: {SeriesName}", seriesItem.Id, seriesItem.Name);
        }
    }
}

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
            var seriesCollection = await apiClient.GetCollectionAsync(seriesItem.Id, cancellationToken);
            var seasons = new List<SeasonSummary>();
            var seasonItems = (seriesCollection.Aggregations ?? [])
                .Where(TvLibraryDiscovery.IsSeason)
                .ToList();

            if (TvLibraryDiscovery.IsSeason(seriesCollection))
            {
                seasonItems.Insert(0, seriesCollection);
            }

            foreach (var seasonItem in seasonItems)
            {
                var detail = await apiClient.GetDetailAsync(seasonItem.Id, cancellationToken);
                seasons.Add(TvLibraryDiscovery.BuildSeasonSummary(seriesItem.Id, seriesItem.Name, detail));
            }

            var series = new SeriesSummary(seriesItem.Id, seriesItem.Name, seasons);
            var firstUri = series.Seasons
                .Select(season => season.FirstMediaUri)
                .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));

            var map = pathMapper.MapSeriesFolder(firstUri);
            if (!map.Success || map.SeriesFolder is null)
            {
                logger.LogWarning("Skipping {SeriesName}: could not map media URI to a series folder.", series.Name);
                continue;
            }

            var previousState = await stateStore.LoadAsync(map.SeriesFolder, cancellationToken);
            var plan = planner.Plan(series, map.SeriesFolder, previousState);
            if (plan.Skip)
            {
                logger.LogWarning("Skipping {SeriesName}: {Reason}", series.Name, plan.SkipReason);
                continue;
            }

            logger.LogInformation(
                "{SeriesName}: {SeriesUnwatched} unwatched episodes, {PosterCount} poster updates planned.",
                series.Name,
                series.UnwatchedCount,
                plan.Items.Count);
        }
    }
}

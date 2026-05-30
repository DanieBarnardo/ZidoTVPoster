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
    private readonly PosterSourceManager sourceManager;
    private readonly PosterRenderer renderer;
    private readonly PosterApplier applier;

    public PosterUpdateWorker(
        ILogger<PosterUpdateWorker> logger,
        IOptions<PosterUpdateOptions> posterOptions,
        IServiceScopeFactory scopeFactory,
        ZidooPathMapper pathMapper,
        PosterStateStore stateStore,
        PosterPlanner planner,
        PosterSourceManager sourceManager,
        PosterRenderer renderer,
        PosterApplier applier)
    {
        this.logger = logger;
        this.posterOptions = posterOptions;
        this.scopeFactory = scopeFactory;
        this.pathMapper = pathMapper;
        this.stateStore = stateStore;
        this.planner = planner;
        this.sourceManager = sourceManager;
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
            try
            {
                await PlanSeriesAsync(apiClient, seriesItem, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Skipping TV series {SeriesId}: {SeriesName} after planning failure.",
                    seriesItem.Id,
                    seriesItem.Name);
            }
        }
    }

    private async Task PlanSeriesAsync(
        ZidooApiClient apiClient,
        ZidooItem seriesItem,
        CancellationToken cancellationToken)
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
            logger.LogWarning(
                "Skipping TV series {SeriesId}: {SeriesName}: could not map media URI {MediaUri} to a series folder.",
                series.SeriesId,
                series.Name,
                firstUri);
            return;
        }

        var previousState = await stateStore.LoadAsync(map.SeriesFolder, cancellationToken);
        var plan = planner.Plan(series, map.SeriesFolder, previousState);
        if (plan.Skip)
        {
            logger.LogWarning(
                "Skipping TV series {SeriesId}: {SeriesName} at {SeriesFolder}: {Reason}",
                series.SeriesId,
                series.Name,
                map.SeriesFolder,
                plan.SkipReason);
            return;
        }

        logger.LogInformation(
            "{SeriesName}: {SeriesUnwatched} unwatched episodes, {PosterCount} poster updates planned.",
            series.Name,
            series.UnwatchedCount,
            plan.Items.Count);

        foreach (var item in plan.Items)
        {
            try
            {
                await ProcessPosterItemAsync(apiClient, item, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Skipping {PosterKind} poster for Zidoo item {ZidooId} after processing failure.",
                    item.Kind,
                    item.ZidooId);
            }
        }
    }

    private async Task ProcessPosterItemAsync(
        ZidooApiClient apiClient,
        PosterUpdateItem item,
        CancellationToken cancellationToken)
    {
        var serviceFolder = Path.Combine(item.SeriesFolder, PosterStateStore.ServiceFolderName);
        var originalFileName = GetOriginalFileName(item);
        var generatedFileName = GetGeneratedFileName(item);
        var originalResult = await sourceManager.EnsureOriginalAsync(
            item.SeriesFolder,
            originalFileName,
            GetLocalPosterCandidates(item),
            apiClient.GetPosterBytesAsync,
            item.ZidooId,
            cancellationToken);

        if (!originalResult.Success || originalResult.OriginalPosterPath is null)
        {
            logger.LogWarning(
                "Skipping {PosterKind} poster for Zidoo item {ZidooId}: {Reason}",
                item.Kind,
                item.ZidooId,
                originalResult.Message);
            return;
        }

        logger.LogInformation(
            "Resolved original {PosterKind} poster for Zidoo item {ZidooId}: {Message}",
            item.Kind,
            item.ZidooId,
            originalResult.Message);

        var generatedPosterPath = Path.Combine(serviceFolder, generatedFileName);
        await renderer.RenderAsync(
            originalResult.OriginalPosterPath,
            generatedPosterPath,
            item.UnwatchedCount,
            posterOptions.Value,
            cancellationToken);

        var applyResult = await applier.ApplyAsync(
            item,
            generatedPosterPath,
            posterOptions.Value.DryRun,
            cancellationToken);

        if (applyResult.Success)
        {
            logger.LogInformation(
                "Applied {PosterKind} poster for Zidoo item {ZidooId}: {Message}",
                item.Kind,
                item.ZidooId,
                applyResult.Message);
            return;
        }

        logger.LogWarning(
            "Failed to apply {PosterKind} poster for Zidoo item {ZidooId}: {Message}",
            item.Kind,
            item.ZidooId,
            applyResult.Message);
    }

    private static string GetOriginalFileName(PosterUpdateItem item)
    {
        return item.Kind == PosterTargetKind.Series
            ? PosterFileNames.OriginalSeriesPoster
            : PosterFileNames.OriginalSeasonPoster(item.SeasonNumber, item.ZidooId);
    }

    private static string GetGeneratedFileName(PosterUpdateItem item)
    {
        return item.Kind == PosterTargetKind.Series
            ? PosterFileNames.GeneratedSeriesPoster
            : PosterFileNames.GeneratedSeasonPoster(item.SeasonNumber, item.ZidooId);
    }

    private static IReadOnlyList<string> GetLocalPosterCandidates(PosterUpdateItem item)
    {
        return item.Kind == PosterTargetKind.Series
            ? [Path.Combine(item.SeriesFolder, "poster.jpg")]
            : [];
    }
}

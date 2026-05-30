using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ConsoleOptions.Parse(args);
            using var cancellation = new CancellationTokenSource();
            global::System.Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            await RunAsync(options, cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            global::System.Console.Error.WriteLine("Cancelled.");
            return 2;
        }
        catch (Exception exception)
        {
            global::System.Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task RunAsync(ConsoleOptions options, CancellationToken cancellationToken)
    {
        var posterOptions = new PosterUpdateOptions { DryRun = options.DryRun };
        var pathMapper = new ZidooPathMapper(options.StorageRoot, options.MediaRootNames);
        var stateStore = new PosterStateStore();
        var planner = new PosterPlanner();
        var sourceManager = new PosterSourceManager();
        var renderer = new PosterRenderer();
        var applier = new PosterApplier();

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var apiClient = new ZidooApiClient(httpClient);

        WriteHeader(options);

        var collections = await apiClient.GetCollectionListAsync(cancellationToken);
        var seriesItems = collections.Data
            .Where(TvLibraryDiscovery.IsTvSeries)
            .Where(item => options.SeriesFilter is null ||
                item.Name.Contains(options.SeriesFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        global::System.Console.WriteLine($"TV series discovered: {seriesItems.Count}");
        global::System.Console.WriteLine();

        foreach (var seriesItem in seriesItems)
        {
            try
            {
                await ProcessSeriesAsync(
                    apiClient,
                    pathMapper,
                    stateStore,
                    planner,
                    sourceManager,
                    renderer,
                    applier,
                    posterOptions,
                    options.ApplyUpdates,
                    seriesItem,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                global::System.Console.WriteLine($"[{seriesItem.Id}] {seriesItem.Name}");
                global::System.Console.WriteLine($"  skipped after error: {exception.Message}");
                global::System.Console.WriteLine();
            }
        }
    }

    private static void WriteHeader(ConsoleOptions options)
    {
        var mode = options.ApplyUpdates ? "update" : "dry-run";
        global::System.Console.WriteLine("ZidoTVPoster debug console");
        global::System.Console.WriteLine($"Mode: {mode}");
        global::System.Console.WriteLine($"Zidoo API: {options.BaseUrl}");
        global::System.Console.WriteLine($"Storage root: {options.StorageRoot}");
        global::System.Console.WriteLine($"Media roots: {string.Join(", ", options.MediaRootNames)}");
        if (!string.IsNullOrWhiteSpace(options.SeriesFilter))
        {
            global::System.Console.WriteLine($"Series filter: {options.SeriesFilter}");
        }

        if (options.ApplyUpdates)
        {
            global::System.Console.WriteLine(
                "Updates enabled: local original posters, generated posters, and state files may be written under each series folder.");
            global::System.Console.WriteLine(
                "Zidoo poster-wall apply is still informational until the poster update route is verified.");
        }
        else
        {
            global::System.Console.WriteLine("Dry run enabled: no poster or state files will be written.");
        }

        global::System.Console.WriteLine();
    }

    private static async Task ProcessSeriesAsync(
        ZidooApiClient apiClient,
        ZidooPathMapper pathMapper,
        PosterStateStore stateStore,
        PosterPlanner planner,
        PosterSourceManager sourceManager,
        PosterRenderer renderer,
        PosterApplier applier,
        PosterUpdateOptions posterOptions,
        bool applyUpdates,
        ZidooItem seriesItem,
        CancellationToken cancellationToken)
    {
        var series = await BuildSeriesSummaryAsync(apiClient, seriesItem, cancellationToken);
        var firstUri = series.Seasons
            .Select(season => season.FirstMediaUri)
            .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));
        var map = pathMapper.MapSeriesFolder(firstUri);

        global::System.Console.WriteLine($"[{series.SeriesId}] {series.Name}");
        global::System.Console.WriteLine($"  unwatched episodes: {series.UnwatchedCount}");
        foreach (var season in series.Seasons.OrderBy(season => season.SeasonNumber))
        {
            global::System.Console.WriteLine(
                $"  season {FormatSeasonNumber(season.SeasonNumber)} [{season.SeasonId}]: {season.UnwatchedCount} unwatched, {season.Episodes.Count} episodes");
        }

        if (!map.Success || map.SeriesFolder is null)
        {
            global::System.Console.WriteLine($"  skipped: could not map media URI to a series folder: {firstUri ?? "<missing>"}");
            global::System.Console.WriteLine();
            return;
        }

        global::System.Console.WriteLine($"  folder: {map.SeriesFolder}");

        var previousState = await stateStore.LoadAsync(map.SeriesFolder, cancellationToken);
        var plan = planner.Plan(series, map.SeriesFolder, previousState);
        if (plan.Skip)
        {
            global::System.Console.WriteLine($"  skipped: {plan.SkipReason}");
            global::System.Console.WriteLine();
            return;
        }

        global::System.Console.WriteLine($"  planned poster updates: {plan.Items.Count}");
        foreach (var item in plan.Items)
        {
            global::System.Console.WriteLine(
                $"    - {item.Kind} [{item.ZidooId}]: {item.UnwatchedCount} unwatched");
        }

        if (plan.Items.Count == 0 || !applyUpdates)
        {
            global::System.Console.WriteLine();
            return;
        }

        var canSaveState = true;
        foreach (var item in plan.Items)
        {
            var result = await ProcessPosterItemAsync(
                apiClient,
                sourceManager,
                renderer,
                applier,
                posterOptions,
                item,
                cancellationToken);

            if (!result)
            {
                canSaveState = false;
            }
        }

        if (canSaveState)
        {
            await stateStore.SaveAsync(map.SeriesFolder, CreatePosterState(series), cancellationToken);
            global::System.Console.WriteLine($"  state saved: {PosterStateStore.GetStatePath(map.SeriesFolder)}");
        }
        else
        {
            global::System.Console.WriteLine("  state not saved because one or more poster updates failed.");
        }

        global::System.Console.WriteLine();
    }

    private static async Task<SeriesSummary> BuildSeriesSummaryAsync(
        ZidooApiClient apiClient,
        ZidooItem seriesItem,
        CancellationToken cancellationToken)
    {
        var seriesCollection = await apiClient.GetCollectionAsync(seriesItem.Id, cancellationToken);
        var seasonItems = (seriesCollection.Aggregations ?? [])
            .Where(TvLibraryDiscovery.IsSeason)
            .ToList();

        if (TvLibraryDiscovery.IsSeason(seriesCollection))
        {
            seasonItems.Insert(0, seriesCollection);
        }

        var seasons = new List<SeasonSummary>();
        foreach (var seasonItem in seasonItems)
        {
            var detail = await apiClient.GetDetailAsync(seasonItem.Id, cancellationToken);
            seasons.Add(TvLibraryDiscovery.BuildSeasonSummary(seriesItem.Id, seriesItem.Name, detail));
        }

        return new SeriesSummary(seriesItem.Id, seriesItem.Name, seasons);
    }

    private static async Task<bool> ProcessPosterItemAsync(
        ZidooApiClient apiClient,
        PosterSourceManager sourceManager,
        PosterRenderer renderer,
        PosterApplier applier,
        PosterUpdateOptions posterOptions,
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
            global::System.Console.WriteLine(
                $"    {item.Kind} [{item.ZidooId}] skipped: {originalResult.Message}");
            return false;
        }

        var generatedPosterPath = Path.Combine(serviceFolder, generatedFileName);
        await renderer.RenderAsync(
            originalResult.OriginalPosterPath,
            generatedPosterPath,
            item.UnwatchedCount,
            posterOptions,
            cancellationToken);

        var applyResult = await applier.ApplyAsync(
            item,
            generatedPosterPath,
            dryRun: true,
            cancellationToken);

        global::System.Console.WriteLine(
            $"    {item.Kind} [{item.ZidooId}] source: {originalResult.Message}");
        global::System.Console.WriteLine($"    {item.Kind} [{item.ZidooId}] rendered: {generatedPosterPath}");
        global::System.Console.WriteLine($"    {item.Kind} [{item.ZidooId}] apply: {applyResult.Message}");
        return applyResult.Success;
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

    private static PosterState CreatePosterState(SeriesSummary series)
    {
        return new PosterState(
            series.SeriesId,
            series.Name,
            DateTimeOffset.UtcNow,
            new PosterCountState(
                series.UnwatchedCount,
                PosterFileNames.OriginalSeriesPoster,
                PosterFileNames.GeneratedSeriesPoster),
            series.Seasons.Select(season => new SeasonPosterState(
                season.SeasonId,
                season.SeasonNumber,
                season.Name,
                season.UnwatchedCount,
                PosterFileNames.OriginalSeasonPoster(season.SeasonNumber, season.SeasonId),
                PosterFileNames.GeneratedSeasonPoster(season.SeasonNumber, season.SeasonId))).ToArray());
    }

    private static string FormatSeasonNumber(int seasonNumber)
    {
        return seasonNumber > 0 ? seasonNumber.ToString("00") : "unknown";
    }
}

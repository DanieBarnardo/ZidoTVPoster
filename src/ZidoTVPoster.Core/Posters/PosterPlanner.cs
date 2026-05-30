using ZidoTVPoster.Core.Library;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterPlanner
{
    public PosterUpdatePlan Plan(SeriesSummary series, string seriesFolder, PosterState? previousState)
    {
        if (!Directory.Exists(seriesFolder))
        {
            return PosterUpdatePlan.Skipped("Series folder does not exist.");
        }

        var items = new List<PosterUpdateItem>();

        if (previousState?.Series.UnwatchedCount != series.UnwatchedCount || !SeriesVisibleArtworkExists(seriesFolder))
        {
            items.Add(PosterUpdateItem.Series(series.SeriesId, series.UnwatchedCount, seriesFolder));
        }

        foreach (var season in series.Seasons)
        {
            var previousSeason = previousState?.Seasons.FirstOrDefault(candidate => candidate.SeasonId == season.SeasonId);
            if (previousSeason?.UnwatchedCount != season.UnwatchedCount ||
                !SeasonVisibleArtworkExists(seriesFolder, season.SeasonNumber))
            {
                items.Add(PosterUpdateItem.Season(
                    season.SeasonId,
                    season.SeasonNumber,
                    season.UnwatchedCount,
                    seriesFolder));
            }
        }

        return PosterUpdatePlan.Ready(items);
    }

    private static bool SeriesVisibleArtworkExists(string seriesFolder)
    {
        return File.Exists(Path.Combine(seriesFolder, "poster.jpg")) &&
            File.Exists(Path.Combine(seriesFolder, "tvshow.nfo"));
    }

    private static bool SeasonVisibleArtworkExists(string seriesFolder, int seasonNumber)
    {
        if (seasonNumber <= 0)
        {
            return false;
        }

        return File.Exists(Path.Combine(seriesFolder, $"season{seasonNumber:00}-poster.jpg"));
    }
}

public sealed record PosterUpdatePlan(bool Skip, string? SkipReason, IReadOnlyList<PosterUpdateItem> Items)
{
    public static PosterUpdatePlan Skipped(string reason)
    {
        return new PosterUpdatePlan(true, reason, []);
    }

    public static PosterUpdatePlan Ready(IReadOnlyList<PosterUpdateItem> items)
    {
        return new PosterUpdatePlan(false, null, items);
    }
}

public sealed record PosterUpdateItem(
    PosterTargetKind Kind,
    int ZidooId,
    int SeasonNumber,
    int UnwatchedCount,
    string SeriesFolder)
{
    public static PosterUpdateItem Series(int zidooId, int unwatchedCount, string seriesFolder)
    {
        return new PosterUpdateItem(PosterTargetKind.Series, zidooId, 0, unwatchedCount, seriesFolder);
    }

    public static PosterUpdateItem Season(int zidooId, int seasonNumber, int unwatchedCount, string seriesFolder)
    {
        return new PosterUpdateItem(PosterTargetKind.Season, zidooId, seasonNumber, unwatchedCount, seriesFolder);
    }
}

public enum PosterTargetKind
{
    Series,
    Season
}

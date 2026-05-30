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

        if (previousState?.Series.UnwatchedCount != series.UnwatchedCount)
        {
            items.Add(PosterUpdateItem.Series(series.SeriesId, series.UnwatchedCount, seriesFolder));
        }

        foreach (var season in series.Seasons)
        {
            var previousSeason = previousState?.Seasons.FirstOrDefault(candidate => candidate.SeasonId == season.SeasonId);
            if (previousSeason?.UnwatchedCount != season.UnwatchedCount)
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

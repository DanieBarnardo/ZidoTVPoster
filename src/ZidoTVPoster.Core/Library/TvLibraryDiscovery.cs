using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Core.Library;

public static class TvLibraryDiscovery
{
    public static bool IsTvSeries(ZidooItem item) => item.Type == 3;

    public static bool IsSeason(ZidooItem item) => item.Type == 4;

    public static SeasonSummary BuildSeasonSummary(int seriesId, string seriesName, ZidooDetailResponse detail)
    {
        var episodes = detail.Episodes
            .Where(item => item.Type == 5)
            .Select(item => new EpisodeSummary(
                item.Id,
                item.Aggregation?.EpisodeNumber ?? 0,
                item.Name,
                item.Watched,
                item.Aggregations?.FirstOrDefault()?.Aggregation?.Uri))
            .ToList();

        return new SeasonSummary(
            detail.Id,
            detail.Aggregation?.SeasonNumber ?? 0,
            detail.Name,
            episodes.Count(episode => !episode.Watched),
            episodes.FirstOrDefault()?.MediaUri,
            episodes);
    }
}

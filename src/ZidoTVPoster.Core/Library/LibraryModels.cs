namespace ZidoTVPoster.Core.Library;

public sealed record SeriesSummary(int SeriesId, string Name, IReadOnlyList<SeasonSummary> Seasons)
{
    public int UnwatchedCount => Seasons.Sum(season => season.UnwatchedCount);
}

public sealed record SeasonSummary(
    int SeasonId,
    int SeasonNumber,
    string Name,
    int UnwatchedCount,
    string? FirstMediaUri,
    IReadOnlyList<EpisodeSummary> Episodes);

public sealed record EpisodeSummary(
    int EpisodeId,
    int EpisodeNumber,
    string Name,
    bool Watched,
    string? MediaUri);

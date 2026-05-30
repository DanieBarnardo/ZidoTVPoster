using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Tests;

public sealed class TvLibraryDiscoveryTests
{
    [Fact]
    public void BuildSeasonSummary_CountsOnlyUnwatchedEpisodes()
    {
        var detail = CreateSeasonDetail(
            CreateEpisode(401, 1, "Currahee", watched: false),
            CreateEpisode(402, 2, "Day of Days", watched: true),
            CreateEpisode(403, 3, "Carentan", watched: false));

        var summary = TvLibraryDiscovery.BuildSeasonSummary(130, "Band of Brothers", detail);

        Assert.Equal(2, summary.UnwatchedCount);
    }

    [Fact]
    public void BuildSeasonSummary_IncludesAllEpisodesAndFirstMediaUri()
    {
        var detail = CreateSeasonDetail(
            CreateEpisode(401, 1, "Currahee", watched: false, "/Series/Band of Brothers/S01E01.mkv"),
            CreateEpisode(402, 2, "Day of Days", watched: true, "/Series/Band of Brothers/S01E02.mkv"),
            CreateNonEpisode(499, "Bonus Feature"));

        var summary = TvLibraryDiscovery.BuildSeasonSummary(130, "Band of Brothers", detail);

        Assert.Equal(131, summary.SeasonId);
        Assert.Equal(1, summary.SeasonNumber);
        Assert.Equal("Season 1", summary.Name);
        Assert.Equal("/Series/Band of Brothers/S01E01.mkv", summary.FirstMediaUri);

        Assert.Collection(
            summary.Episodes,
            episode =>
            {
                Assert.Equal(401, episode.EpisodeId);
                Assert.Equal(1, episode.EpisodeNumber);
                Assert.Equal("Currahee", episode.Name);
                Assert.False(episode.Watched);
                Assert.Equal("/Series/Band of Brothers/S01E01.mkv", episode.MediaUri);
            },
            episode =>
            {
                Assert.Equal(402, episode.EpisodeId);
                Assert.Equal(2, episode.EpisodeNumber);
                Assert.Equal("Day of Days", episode.Name);
                Assert.True(episode.Watched);
                Assert.Equal("/Series/Band of Brothers/S01E02.mkv", episode.MediaUri);
            });
    }

    [Fact]
    public void SeriesSummary_UnwatchedCount_SumsSeasonCounts()
    {
        var series = new SeriesSummary(
            130,
            "Band of Brothers",
            [
                new SeasonSummary(131, 1, "Season 1", 2, null, []),
                new SeasonSummary(132, 2, "Season 2", 3, null, [])
            ]);

        Assert.Equal(5, series.UnwatchedCount);
    }

    [Fact]
    public void IsTvSeriesAndIsSeason_UseZidooTypes()
    {
        var series = new ZidooItem(130, -1, 3, "Band of Brothers", false);
        var season = new ZidooItem(131, 130, 4, "Season 1", false);
        var movie = new ZidooItem(201, -1, 2, "Movie Collection", false);

        Assert.True(TvLibraryDiscovery.IsTvSeries(series));
        Assert.True(TvLibraryDiscovery.IsSeason(season));
        Assert.False(TvLibraryDiscovery.IsTvSeries(movie));
        Assert.False(TvLibraryDiscovery.IsSeason(movie));
    }

    private static ZidooDetailResponse CreateSeasonDetail(params ZidooItem[] episodes)
    {
        return new ZidooDetailResponse(
            131,
            130,
            4,
            "Season 1",
            false,
            new ZidooAggregation(SeasonNumber: 1),
            episodes,
            null);
    }

    private static ZidooItem CreateEpisode(int id, int episodeNumber, string name, bool watched, string? mediaUri = null)
    {
        var media = mediaUri is null
            ? null
            : new[]
            {
                new ZidooItem(
                    id + 1000,
                    id,
                    0,
                    $"{name}.mkv",
                    Watched: false,
                    Aggregation: new ZidooAggregation(Uri: mediaUri))
            };

        return new ZidooItem(
            id,
            131,
            5,
            name,
            watched,
            new ZidooAggregation(EpisodeNumber: episodeNumber),
            media);
    }

    private static ZidooItem CreateNonEpisode(int id, string name)
    {
        return new ZidooItem(id, 131, 1, name, Watched: false);
    }
}

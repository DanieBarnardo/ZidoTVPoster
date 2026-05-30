using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterPlannerTests
{
    [Fact]
    public void Plan_SkipsMissingSeriesFolder()
    {
        var series = CreateSeries();
        var seriesFolder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"), "MissingSeries");
        var planner = new PosterPlanner();

        var plan = planner.Plan(series, seriesFolder, previousState: null);

        Assert.True(plan.Skip);
        Assert.Equal("Series folder does not exist.", plan.SkipReason);
        Assert.Empty(plan.Items);
        Assert.False(Directory.Exists(seriesFolder));
    }

    [Fact]
    public void Plan_IncludesSeriesAndSeasonItems_WhenFolderExistsAndPreviousStateIsMissing()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var series = CreateSeries();
        var planner = new PosterPlanner();

        var plan = planner.Plan(series, seriesFolder, previousState: null);

        Assert.False(plan.Skip);
        Assert.Null(plan.SkipReason);
        Assert.Collection(
            plan.Items,
            item =>
            {
                Assert.Equal(PosterTargetKind.Series, item.Kind);
                Assert.Equal(42, item.ZidooId);
                Assert.Equal(0, item.SeasonNumber);
                Assert.Equal(5, item.UnwatchedCount);
                Assert.Equal(seriesFolder, item.SeriesFolder);
            },
            item =>
            {
                Assert.Equal(PosterTargetKind.Season, item.Kind);
                Assert.Equal(43, item.ZidooId);
                Assert.Equal(1, item.SeasonNumber);
                Assert.Equal(2, item.UnwatchedCount);
                Assert.Equal(seriesFolder, item.SeriesFolder);
            },
            item =>
            {
                Assert.Equal(PosterTargetKind.Season, item.Kind);
                Assert.Equal(44, item.ZidooId);
                Assert.Equal(2, item.SeasonNumber);
                Assert.Equal(3, item.UnwatchedCount);
                Assert.Equal(seriesFolder, item.SeriesFolder);
            });
    }

    [Fact]
    public void Plan_IncludesNoItems_WhenPreviousStateCountsMatchCurrentCounts()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var series = CreateSeries();
        var previousState = CreateState(seriesUnwatchedCount: 5, seasonOneUnwatchedCount: 2, seasonTwoUnwatchedCount: 3);
        var planner = new PosterPlanner();

        var plan = planner.Plan(series, seriesFolder, previousState);

        Assert.False(plan.Skip);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void Plan_IncludesOnlyChangedSeasons_WhenSeriesTotalIsUnchanged()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var series = CreateSeries(seasonOneUnwatchedCount: 3, seasonTwoUnwatchedCount: 2);
        var previousState = CreateState(seriesUnwatchedCount: 5, seasonOneUnwatchedCount: 2, seasonTwoUnwatchedCount: 3);
        var planner = new PosterPlanner();

        var plan = planner.Plan(series, seriesFolder, previousState);

        Assert.False(plan.Skip);
        Assert.DoesNotContain(plan.Items, item => item.Kind == PosterTargetKind.Series);
        Assert.Collection(
            plan.Items,
            item =>
            {
                Assert.Equal(PosterTargetKind.Season, item.Kind);
                Assert.Equal(43, item.ZidooId);
                Assert.Equal(1, item.SeasonNumber);
                Assert.Equal(3, item.UnwatchedCount);
                Assert.Equal(seriesFolder, item.SeriesFolder);
            },
            item =>
            {
                Assert.Equal(PosterTargetKind.Season, item.Kind);
                Assert.Equal(44, item.ZidooId);
                Assert.Equal(2, item.SeasonNumber);
                Assert.Equal(2, item.UnwatchedCount);
                Assert.Equal(seriesFolder, item.SeriesFolder);
            });
    }

    private static SeriesSummary CreateSeries(int seasonOneUnwatchedCount = 2, int seasonTwoUnwatchedCount = 3)
    {
        return new SeriesSummary(
            42,
            "Band of Brothers",
            [
                new SeasonSummary(43, 1, "Season 1", seasonOneUnwatchedCount, null, []),
                new SeasonSummary(44, 2, "Season 2", seasonTwoUnwatchedCount, null, [])
            ]);
    }

    private static PosterState CreateState(
        int seriesUnwatchedCount,
        int seasonOneUnwatchedCount,
        int seasonTwoUnwatchedCount)
    {
        return new PosterState(
            42,
            "Band of Brothers",
            new DateTimeOffset(2026, 5, 30, 10, 15, 0, TimeSpan.Zero),
            new PosterCountState(seriesUnwatchedCount, "series-original.jpg", "series-generated.jpg"),
            [
                new SeasonPosterState(43, 1, "Season 1", seasonOneUnwatchedCount, "s1-original.jpg", "s1-generated.jpg"),
                new SeasonPosterState(44, 2, "Season 2", seasonTwoUnwatchedCount, "s2-original.jpg", "s2-generated.jpg")
            ]);
    }

    private static string CreateTempSeriesFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

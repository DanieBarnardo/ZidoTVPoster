using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterStateStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenStateFileIsMissing()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var store = new PosterStateStore();

        var state = await store.LoadAsync(seriesFolder, CancellationToken.None);

        Assert.Null(state);
    }

    [Fact]
    public async Task SaveAsync_CreatesServiceFolderUnderProvidedSeriesFolder()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var store = new PosterStateStore();

        await store.SaveAsync(seriesFolder, CreateState(), CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName)));
        Assert.True(File.Exists(PosterStateStore.GetStatePath(seriesFolder)));
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripPosterState()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var store = new PosterStateStore();
        var expected = CreateState();

        await store.SaveAsync(seriesFolder, expected, CancellationToken.None);
        var actual = await store.LoadAsync(seriesFolder, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(expected.SeriesId, actual.SeriesId);
        Assert.Equal(expected.SeriesName, actual.SeriesName);
        Assert.Equal(expected.LastUpdatedUtc, actual.LastUpdatedUtc);
        Assert.Equal(expected.Series, actual.Series);
        Assert.Equal(expected.Seasons, actual.Seasons);
    }

    [Fact]
    public async Task SaveAsync_WritesIndentedJsonToStateFile()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var store = new PosterStateStore();

        await store.SaveAsync(seriesFolder, CreateState(), CancellationToken.None);

        var json = await File.ReadAllTextAsync(PosterStateStore.GetStatePath(seriesFolder), CancellationToken.None);
        Assert.Contains(Environment.NewLine, json);
        Assert.Contains("  \"SeriesId\"", json);
    }

    [Fact]
    public async Task SaveAsync_DoesNotCreateParentSeriesFolder()
    {
        var seriesFolder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"), "MissingSeries");
        var store = new PosterStateStore();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => store.SaveAsync(seriesFolder, CreateState(), CancellationToken.None));

        Assert.False(Directory.Exists(seriesFolder));
    }

    [Fact]
    public async Task SaveAsync_DoesNotReplaceExistingStateFile_WhenTokenIsPreCanceled()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var serviceFolder = Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName);
        Directory.CreateDirectory(serviceFolder);
        var statePath = PosterStateStore.GetStatePath(seriesFolder);
        var originalJson = """
            {
              "SeriesId": 7,
              "SeriesName": "Existing"
            }
            """;
        await File.WriteAllTextAsync(statePath, originalJson, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var store = new PosterStateStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(seriesFolder, CreateState(), cts.Token));

        var actualJson = await File.ReadAllTextAsync(statePath, CancellationToken.None);
        Assert.Equal(originalJson, actualJson);
    }

    private static PosterState CreateState()
    {
        return new PosterState(
            42,
            "Band of Brothers",
            new DateTimeOffset(2026, 5, 30, 10, 15, 0, TimeSpan.Zero),
            new PosterCountState(3, "series-original.jpg", "series-generated.jpg"),
            [
                new SeasonPosterState(43, 1, "Season 1", 2, "s1-original.jpg", "s1-generated.jpg"),
                new SeasonPosterState(44, 2, "Season 2", 1, "s2-original.jpg", "s2-generated.jpg")
            ]);
    }

    private static string CreateTempSeriesFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

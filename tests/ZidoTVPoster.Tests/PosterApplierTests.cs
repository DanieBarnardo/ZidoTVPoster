using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterApplierTests
{
    [Fact]
    public async Task ApplyAsync_ReturnsSuccessWithDryRunMessage_WhenDryRunIsEnabled()
    {
        var item = PosterUpdateItem.Series(42, 5, @"Z:\TV\Band of Brothers");
        var generatedPosterPath = Path.Combine(
            Path.GetTempPath(),
            "ZidoTVPosterTests",
            Guid.NewGuid().ToString("N"),
            "missing-poster.jpg");
        var applier = new PosterApplier();

        var result = await applier.ApplyAsync(item, generatedPosterPath, dryRun: true, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Dry run", result.Message);
        Assert.Contains(generatedPosterPath, result.Message);
        Assert.False(File.Exists(generatedPosterPath));
    }

    [Fact]
    public async Task ApplyAsync_WritesSeriesPosterAndTvShowNfo_WhenDryRunIsDisabled()
    {
        var seriesFolder = CreateTempFolder();
        var generatedPosterPath = Path.Combine(seriesFolder, ".zido-tv-poster", "generated-series-poster.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(generatedPosterPath)!);
        await File.WriteAllTextAsync(generatedPosterPath, "poster");
        var item = PosterUpdateItem.Series(42, 5, seriesFolder);
        var applier = new PosterApplier();

        var result = await applier.ApplyAsync(item, generatedPosterPath, dryRun: false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("poster", await File.ReadAllTextAsync(Path.Combine(seriesFolder, "poster.jpg")));
        var nfo = await File.ReadAllTextAsync(Path.Combine(seriesFolder, "tvshow.nfo"));
        Assert.Contains("<title>" + Path.GetFileName(seriesFolder) + "</title>", nfo);
        Assert.Contains("<uniqueid type=\"zidoo\" default=\"true\">zidoo-42</uniqueid>", nfo);
    }

    [Fact]
    public async Task ApplyAsync_WritesSeasonPosterAliases_WhenDryRunIsDisabled()
    {
        var seriesFolder = CreateTempFolder();
        var seasonFolder = Path.Combine(seriesFolder, "Season 1");
        Directory.CreateDirectory(seasonFolder);
        var generatedPosterPath = Path.Combine(seriesFolder, ".zido-tv-poster", "generated-season-01-poster.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(generatedPosterPath)!);
        await File.WriteAllTextAsync(generatedPosterPath, "season poster");
        var item = PosterUpdateItem.Season(43, 1, 2, seriesFolder);
        var applier = new PosterApplier();

        var result = await applier.ApplyAsync(item, generatedPosterPath, dryRun: false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("season poster", await File.ReadAllTextAsync(Path.Combine(seriesFolder, "season01-poster.jpg")));
        Assert.Equal("season poster", await File.ReadAllTextAsync(Path.Combine(seasonFolder, "poster.jpg")));
    }

    [Fact]
    public async Task ApplyAsync_ThrowsOperationCanceledException_WhenTokenIsCanceled()
    {
        var item = PosterUpdateItem.Series(42, 5, @"Z:\TV\Band of Brothers");
        var applier = new PosterApplier();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            applier.ApplyAsync(item, "generated-poster.jpg", dryRun: true, cancellationTokenSource.Token));
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

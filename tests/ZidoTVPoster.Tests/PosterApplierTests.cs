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
    public async Task ApplyAsync_ReturnsFailureUntilZidooRouteIsVerified_WhenDryRunIsDisabled()
    {
        var item = PosterUpdateItem.Season(43, 1, 2, @"Z:\TV\Band of Brothers");
        var applier = new PosterApplier();

        var result = await applier.ApplyAsync(item, "generated-poster.jpg", dryRun: false, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not enabled", result.Message);
        Assert.Contains("Zidoo poster update route is verified", result.Message);
    }
}

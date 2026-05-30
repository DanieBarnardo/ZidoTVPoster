using ZidoTVPoster.Core.Configuration;

namespace ZidoTVPoster.Tests;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void ZidooOptions_UsesSeriesAsDefaultMediaRoot()
    {
        var options = new ZidooOptions();

        Assert.Equal("http://192.168.0.209:9529", options.BaseUrl);
        Assert.Contains("Series", options.MediaRootNames);
        Assert.Equal(10, options.RequestTimeoutSeconds);
    }

    [Fact]
    public void PosterUpdateOptions_DefaultsToDryRun()
    {
        var options = new PosterUpdateOptions();

        Assert.True(options.DryRun);
        Assert.True(options.HideBadgeWhenZero);
        Assert.Equal(60, options.PollIntervalSeconds);
        Assert.Equal("{0} unwatched", options.BadgeTextFormat);
    }
}

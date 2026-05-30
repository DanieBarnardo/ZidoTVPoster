using ZidoTVPoster.Core.Configuration;

namespace ZidoTVPoster.Tests;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void ZidooOptions_UsesSeriesAsDefaultMediaRoot()
    {
        var options = new ZidooOptions();

        Assert.Equal("http://192.168.0.209:9529", options.BaseUrl);
        Assert.Equal(@"D:\MediaData\Series", options.StorageRoot);
        Assert.Equal(["Series"], options.MediaRootNames);
        Assert.Equal(10, options.RequestTimeoutSeconds);
        Assert.True(ZidooOptions.IsValid(options));
    }

    [Fact]
    public void PosterUpdateOptions_DefaultsToDryRun()
    {
        var options = new PosterUpdateOptions();

        Assert.True(options.DryRun);
        Assert.True(options.HideBadgeWhenZero);
        Assert.Equal(60, options.PollIntervalSeconds);
        Assert.Equal("TopRight", options.BadgePlacement);
        Assert.Equal("{0} unwatched", options.BadgeTextFormat);
        Assert.True(PosterUpdateOptions.IsValid(options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    public void ZidooOptions_RejectsInvalidBaseUrl(string baseUrl)
    {
        var options = new ZidooOptions { BaseUrl = baseUrl };

        Assert.False(ZidooOptions.IsValid(options));
    }

    [Fact]
    public void ZidooOptions_RejectsInvalidStorageConfiguration()
    {
        Assert.False(ZidooOptions.IsValid(new ZidooOptions { StorageRoot = " " }));
        Assert.False(ZidooOptions.IsValid(new ZidooOptions { MediaRootNames = [] }));
        Assert.False(ZidooOptions.IsValid(new ZidooOptions { MediaRootNames = ["Series", " "] }));
        Assert.False(ZidooOptions.IsValid(new ZidooOptions { RequestTimeoutSeconds = 0 }));
    }

    [Fact]
    public void PosterUpdateOptions_RejectsInvalidPollingAndBadgeFormat()
    {
        Assert.False(PosterUpdateOptions.IsValid(new PosterUpdateOptions { PollIntervalSeconds = 0 }));
        Assert.False(PosterUpdateOptions.IsValid(new PosterUpdateOptions { BadgeTextFormat = " " }));
        Assert.False(PosterUpdateOptions.IsValid(new PosterUpdateOptions { BadgeTextFormat = "{0 unwatched" }));
    }
}

using ZidoTVPoster.Core.Storage;

namespace ZidoTVPoster.Tests;

public sealed class ZidooPathMapperTests
{
    [Fact]
    public void MapSeriesFolder_MapsSeriesUriToStorageFolder()
    {
        var result = ZidooPathMapper.MapSeriesFolder(
            "/Series/Band of Brothers/Season 1/S01E01.mkv",
            @"D:\MediaData\Series",
            ["Series"]);

        Assert.True(result.Success);
        Assert.Equal(@"D:\MediaData\Series\Band of Brothers", result.SeriesFolder);
    }

    [Fact]
    public void MapSeriesFolder_MapsConfiguredTvRoot()
    {
        var result = ZidooPathMapper.MapSeriesFolder(
            "/TV/Show Name/Season 1/S01E01.mkv",
            @"D:\MediaData\Series",
            ["Series", "TV"]);

        Assert.True(result.Success);
        Assert.Equal(@"D:\MediaData\Series\Show Name", result.SeriesFolder);
    }

    [Fact]
    public void MapSeriesFolder_ReturnsFailureForUnknownMediaRoot()
    {
        var result = ZidooPathMapper.MapSeriesFolder(
            "/Downloads/Show/Season 1/S01E01.mkv",
            @"\\192.168.0.209\Share\Storage",
            ["Series", "TV"]);

        Assert.False(result.Success);
        Assert.Null(result.SeriesFolder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapSeriesFolder_ReturnsFailureForMissingUri(string? uri)
    {
        var result = ZidooPathMapper.MapSeriesFolder(
            uri,
            @"\\192.168.0.209\Share\Storage",
            ["Series"]);

        Assert.False(result.Success);
        Assert.Null(result.SeriesFolder);
    }
}

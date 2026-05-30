using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterFileNamesTests
{
    [Fact]
    public void OriginalSeasonPoster_UsesTwoDigitSeasonNumber_WhenSeasonNumberIsPositive()
    {
        var fileName = PosterFileNames.OriginalSeasonPoster(1, 131);

        Assert.Equal("original-season-01-poster.jpg", fileName);
    }

    [Fact]
    public void GeneratedSeasonPoster_UsesTwoDigitSeasonNumber_WhenSeasonNumberIsPositive()
    {
        var fileName = PosterFileNames.GeneratedSeasonPoster(2, 131);

        Assert.Equal("generated-season-02-poster.jpg", fileName);
    }

    [Fact]
    public void OriginalSeasonPoster_UsesZidooFallback_WhenSeasonNumberIsNotPositive()
    {
        var fileName = PosterFileNames.OriginalSeasonPoster(0, 131);

        Assert.Equal("original-season-zidoo-131-poster.jpg", fileName);
    }

    [Fact]
    public void GeneratedSeasonPoster_UsesZidooFallback_WhenSeasonNumberIsNotPositive()
    {
        var fileName = PosterFileNames.GeneratedSeasonPoster(-1, 131);

        Assert.Equal("generated-season-zidoo-131-poster.jpg", fileName);
    }
}

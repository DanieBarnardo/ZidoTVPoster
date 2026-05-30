using ZidoTVPoster.Console;

namespace ZidoTVPoster.Tests;

public sealed class ConsoleOptionsTests
{
    [Fact]
    public void Parse_DefaultsToApplyUpdates()
    {
        var options = ConsoleOptions.Parse([]);

        Assert.False(options.DryRun);
        Assert.True(options.ApplyUpdates);
        Assert.Null(options.SeriesFilter);
    }

    [Fact]
    public void Parse_DryRunDisablesApplyUpdates()
    {
        var options = ConsoleOptions.Parse(["--dry-run"]);

        Assert.True(options.DryRun);
        Assert.False(options.ApplyUpdates);
    }

    [Fact]
    public void Parse_ReadsSeriesFilter()
    {
        var options = ConsoleOptions.Parse(["--series", "Band of Brothers"]);

        Assert.Equal("Band of Brothers", options.SeriesFilter);
    }
}

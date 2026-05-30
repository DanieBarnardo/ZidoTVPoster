using ZidoTVPoster.Console;
using ZidoTVPoster.Core.Configuration;

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

    [Fact]
    public void Parse_ReadsZidooConfigAndLetsCommandLineOverride()
    {
        var configured = new ZidooOptions
        {
            BaseUrl = "http://configured:9529",
            StorageRoot = @"D:\MediaData\Series",
            MediaRootNames = ["Series"],
            RequestTimeoutSeconds = 15
        };

        var options = ConsoleOptions.Parse(
            ["--storage-root", @"E:\OtherMedia", "--media-roots", "TV Shows,Archive"],
            configured);

        Assert.Equal("http://configured:9529", options.BaseUrl);
        Assert.Equal(@"E:\OtherMedia", options.StorageRoot);
        Assert.Equal(["TV Shows", "Archive"], options.MediaRootNames);
        Assert.Equal(15, options.RequestTimeoutSeconds);
    }

    [Fact]
    public void LoadZidooOptions_ReadsAppSettingsFromConfiguredFolder()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"zido-console-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);
        try
        {
            File.WriteAllText(
                Path.Combine(tempFolder, "appsettings.json"),
                """
                {
                  "Zidoo": {
                    "BaseUrl": "http://json-config:9529",
                    "StorageRoot": "D:\\MediaData\\Series",
                    "MediaRootNames": [ "Series" ],
                    "RequestTimeoutSeconds": 22
                  }
                }
                """);

            var options = ConsoleConfiguration.LoadZidooOptions(tempFolder);

            Assert.Equal("http://json-config:9529", options.BaseUrl);
            Assert.Equal(@"D:\MediaData\Series", options.StorageRoot);
            Assert.Equal(["Series"], options.MediaRootNames);
            Assert.Equal(22, options.RequestTimeoutSeconds);
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }
}

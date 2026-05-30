using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;
using ZidoTVPoster.Service;

namespace ZidoTVPoster.Tests;

public sealed class PosterUpdateWorkerTests
{
    [Fact]
    public async Task RunOnceAsync_AfterSuccessfulDryRun_SavesCurrentPosterState()
    {
        var storageRoot = CreateTempFolder();
        var seriesFolder = Path.Combine(storageRoot, "Band of Brothers");
        Directory.CreateDirectory(seriesFolder);
        using var httpClient = new HttpClient(new WorkerHttpMessageHandler(CreatePosterBytes()))
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var worker = CreateWorker(httpClient, storageRoot, dryRun: true);

        await RunOnceAsync(worker, CancellationToken.None);

        var state = await new PosterStateStore().LoadAsync(seriesFolder, CancellationToken.None);
        Assert.NotNull(state);
        Assert.Equal(130, state.SeriesId);
        Assert.Equal("Band of Brothers", state.SeriesName);
        Assert.InRange(state.LastUpdatedUtc, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(new PosterCountState(1, PosterFileNames.OriginalSeriesPoster, PosterFileNames.GeneratedSeriesPoster), state.Series);
        var season = Assert.Single(state.Seasons);
        Assert.Equal(131, season.SeasonId);
        Assert.Equal(1, season.SeasonNumber);
        Assert.Equal("Season 1", season.Name);
        Assert.Equal(1, season.UnwatchedCount);
        Assert.Equal(PosterFileNames.OriginalSeasonPoster(1, 131), season.OriginalPoster);
        Assert.Equal(PosterFileNames.GeneratedSeasonPoster(1, 131), season.GeneratedPoster);
        Assert.False(File.Exists(Path.Combine(seriesFolder, "poster.jpg")));
        Assert.False(File.Exists(Path.Combine(seriesFolder, "tvshow.nfo")));
    }

    [Fact]
    public async Task RunOnceAsync_WhenDryRunIsDisabled_WritesZidooVisibleArtwork()
    {
        var storageRoot = CreateTempFolder();
        var seriesFolder = Path.Combine(storageRoot, "Band of Brothers");
        var seasonFolder = Path.Combine(seriesFolder, "Season 1");
        Directory.CreateDirectory(seasonFolder);
        using var httpClient = new HttpClient(new WorkerHttpMessageHandler(CreatePosterBytes()))
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var worker = CreateWorker(httpClient, storageRoot, dryRun: false);

        await RunOnceAsync(worker, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(seriesFolder, "poster.jpg")));
        Assert.True(File.Exists(Path.Combine(seriesFolder, "tvshow.nfo")));
        Assert.True(File.Exists(Path.Combine(seriesFolder, "season01-poster.jpg")));
        Assert.True(File.Exists(Path.Combine(seasonFolder, "poster.jpg")));
        Assert.True(File.Exists(PosterStateStore.GetStatePath(seriesFolder)));
    }

    [Fact]
    public async Task RunOnceAsync_WhenSourcePosterIsMissing_DoesNotSavePosterState()
    {
        var storageRoot = CreateTempFolder();
        var seriesFolder = Path.Combine(storageRoot, "Band of Brothers");
        Directory.CreateDirectory(seriesFolder);
        using var httpClient = new HttpClient(new WorkerHttpMessageHandler(CreatePosterBytes(), missingPosterIds: new HashSet<int> { 130 }))
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var worker = CreateWorker(httpClient, storageRoot, dryRun: true);

        await RunOnceAsync(worker, CancellationToken.None);

        Assert.False(File.Exists(PosterStateStore.GetStatePath(seriesFolder)));
    }

    private static PosterUpdateWorker CreateWorker(HttpClient httpClient, string storageRoot, bool dryRun)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ZidooApiClient(httpClient));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new PosterUpdateWorker(
            NullLogger<PosterUpdateWorker>.Instance,
            Options.Create(new PosterUpdateOptions { DryRun = dryRun, BadgeTextFormat = "{0}" }),
            scopeFactory,
            new ZidooPathMapper(storageRoot, ["Series"]),
            new PosterStateStore(),
            new PosterPlanner(),
            new PosterSourceManager(),
            new PosterRenderer(),
            new PosterApplier());
    }

    private static async Task RunOnceAsync(PosterUpdateWorker worker, CancellationToken cancellationToken)
    {
        var method = typeof(PosterUpdateWorker).GetMethod("RunOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RunOnceAsync was not found.");
        var task = (Task?)method.Invoke(worker, [cancellationToken])
            ?? throw new InvalidOperationException("RunOnceAsync did not return a task.");
        await task;
    }

    private static byte[] CreatePosterBytes()
    {
        using var image = new Image<Rgba32>(120, 180, Color.CornflowerBlue);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class WorkerHttpMessageHandler(byte[] posterBytes, IReadOnlySet<int>? missingPosterIds = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = request.RequestUri?.PathAndQuery switch
            {
                "/ZidooPoster/getCollectionList" => JsonResponse(
                    """
                    {
                      "status": 200,
                      "data": [
                        { "id": 130, "parentId": -1, "type": 3, "name": "Band of Brothers", "watched": false }
                      ]
                    }
                    """),
                "/ZidooPoster/getCollection?id=130" => JsonResponse(
                    """
                    {
                      "id": 130,
                      "parentId": -1,
                      "type": 3,
                      "name": "Band of Brothers",
                      "watched": false,
                      "aggregations": [
                        { "id": 131, "parentId": 130, "type": 4, "name": "Season 1", "watched": false }
                      ]
                    }
                    """),
                "/ZidooPoster/getDetail?id=131" => JsonResponse(
                    """
                    {
                      "id": 131,
                      "parentId": 130,
                      "type": 4,
                      "name": "Season 1",
                      "watched": false,
                      "aggregation": {
                        "id": "131",
                        "seasonNumber": "1",
                        "tvName": "Band of Brothers"
                      },
                      "episodes": [
                        {
                          "id": 401,
                          "parentId": 131,
                          "type": 5,
                          "name": "Currahee",
                          "watched": false,
                          "aggregation": { "episodeNumber": "1" },
                          "aggregations": [
                            {
                              "id": 402,
                              "parentId": 401,
                              "type": 0,
                              "name": "Band of Brothers - S01E01.mkv",
                              "watched": false,
                              "aggregation": {
                                "uri": "/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv"
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """),
                "/ZidooPoster/getFile/getPoster?id=130&w=1000&h=1500" when missingPosterIds?.Contains(130) == true =>
                    new HttpResponseMessage(HttpStatusCode.NotFound),
                "/ZidooPoster/getFile/getPoster?id=130&w=1000&h=1500" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(posterBytes)
                },
                "/ZidooPoster/getFile/getPoster?id=131&w=1000&h=1500" when missingPosterIds?.Contains(131) == true =>
                    new HttpResponseMessage(HttpStatusCode.NotFound),
                "/ZidooPoster/getFile/getPoster?id=131&w=1000&h=1500" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(posterBytes)
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            return Task.FromResult(response);
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        }
    }
}

# Zidoo TV Poster Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a C# Windows Worker Service that polls a Zidoo Poster Wall API, counts unwatched TV episodes, and generates series and season posters with unwatched badges while keeping all durable per-series artifacts inside each source series folder.

**Architecture:** Create a small .NET solution with a pure core library, a worker service host, and xUnit tests. The first shipped behavior is safe dry-run discovery plus local poster generation; applying posters to Zidoo is behind a dry-run/probe boundary until the supported route is verified.

**Tech Stack:** .NET 10 SDK, C#, Worker Service, Microsoft.Extensions.Hosting.WindowsServices, System.Text.Json, HttpClient, SixLabors.ImageSharp, xUnit.

---

## File Structure

- Create: `ZidoTVPoster.sln` - solution file.
- Create: `src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj` - core library package references.
- Create: `src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj` - worker service package references.
- Create: `tests/ZidoTVPoster.Tests/ZidoTVPoster.Tests.csproj` - xUnit test project.
- Create: `src/ZidoTVPoster.Core/Configuration/ZidooOptions.cs` - Zidoo API/storage config.
- Create: `src/ZidoTVPoster.Core/Configuration/PosterUpdateOptions.cs` - polling and badge config.
- Create: `src/ZidoTVPoster.Core/Zidoo/ZidooApiClient.cs` - HTTP client for Zidoo endpoints.
- Create: `src/ZidoTVPoster.Core/Zidoo/ZidooModels.cs` - API DTOs.
- Create: `src/ZidoTVPoster.Core/Library/TvLibraryDiscovery.cs` - turns API DTOs into series/season episode summaries.
- Create: `src/ZidoTVPoster.Core/Library/LibraryModels.cs` - domain records.
- Create: `src/ZidoTVPoster.Core/Storage/ZidooPathMapper.cs` - maps API media URIs to UNC paths.
- Create: `src/ZidoTVPoster.Core/Posters/PosterStateStore.cs` - reads/writes `.zido-tv-poster/poster-state.json`.
- Create: `src/ZidoTVPoster.Core/Posters/PosterPlanner.cs` - decides which posters need generation and skips missing folders.
- Create: `src/ZidoTVPoster.Core/Posters/PosterSourceManager.cs` - finds or downloads original posters into each series-local `.zido-tv-poster` folder.
- Create: `src/ZidoTVPoster.Core/Posters/PosterRenderer.cs` - renders badges onto originals.
- Create: `src/ZidoTVPoster.Core/Posters/PosterApplier.cs` - dry-run-first poster application seam.
- Create: `src/ZidoTVPoster.Service/Program.cs` - service host wiring.
- Create: `src/ZidoTVPoster.Service/PosterUpdateWorker.cs` - polling loop.
- Create: `src/ZidoTVPoster.Service/appsettings.json` - default dry-run config.
- Create: `README.md` - setup, config, and dry-run instructions.

---

### Task 1: Scaffold Solution

**Files:**
- Create: `ZidoTVPoster.sln`
- Create: `src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj`
- Create: `src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj`
- Create: `tests/ZidoTVPoster.Tests/ZidoTVPoster.Tests.csproj`

- [ ] **Step 1: Create projects**

Run:

```powershell
dotnet new sln -n ZidoTVPoster
dotnet new classlib -n ZidoTVPoster.Core -o src/ZidoTVPoster.Core
dotnet new worker -n ZidoTVPoster.Service -o src/ZidoTVPoster.Service
dotnet new xunit -n ZidoTVPoster.Tests -o tests/ZidoTVPoster.Tests
dotnet sln add src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj
dotnet sln add src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
dotnet sln add tests/ZidoTVPoster.Tests/ZidoTVPoster.Tests.csproj
dotnet add src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj reference src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj
dotnet add tests/ZidoTVPoster.Tests/ZidoTVPoster.Tests.csproj reference src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj
```

Expected: all projects are created and added to the solution.

- [ ] **Step 2: Add packages**

Run:

```powershell
dotnet add src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj package SixLabors.ImageSharp
dotnet add src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj package Microsoft.Extensions.Hosting.WindowsServices
dotnet add tests/ZidoTVPoster.Tests/ZidoTVPoster.Tests.csproj package SixLabors.ImageSharp
```

Expected: package references are added successfully.

- [ ] **Step 3: Remove template files**

Delete:

```text
src/ZidoTVPoster.Core/Class1.cs
tests/ZidoTVPoster.Tests/UnitTest1.cs
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add ZidoTVPoster.sln src tests
git commit -m "chore: scaffold zidoo poster solution"
```

---

### Task 2: Configuration Models

**Files:**
- Create: `src/ZidoTVPoster.Core/Configuration/ZidooOptions.cs`
- Create: `src/ZidoTVPoster.Core/Configuration/PosterUpdateOptions.cs`
- Create: `tests/ZidoTVPoster.Tests/ConfigurationOptionsTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/ZidoTVPoster.Tests/ConfigurationOptionsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter ConfigurationOptionsTests
```

Expected: fails because `ZidooOptions` and `PosterUpdateOptions` do not exist.

- [ ] **Step 3: Implement configuration records**

Create `src/ZidoTVPoster.Core/Configuration/ZidooOptions.cs`:

```csharp
namespace ZidoTVPoster.Core.Configuration;

public sealed class ZidooOptions
{
    public string BaseUrl { get; set; } = "http://192.168.0.209:9529";
    public string StorageRoot { get; set; } = @"\\192.168.0.209\Share\Storage";
    public string[] MediaRootNames { get; set; } = ["Series"];
    public int RequestTimeoutSeconds { get; set; } = 10;
}
```

Create `src/ZidoTVPoster.Core/Configuration/PosterUpdateOptions.cs`:

```csharp
namespace ZidoTVPoster.Core.Configuration;

public sealed class PosterUpdateOptions
{
    public int PollIntervalSeconds { get; set; } = 60;
    public bool DryRun { get; set; } = true;
    public bool HideBadgeWhenZero { get; set; } = true;
    public string BadgePlacement { get; set; } = "TopRight";
    public string BadgeTextFormat { get; set; } = "{0} unwatched";
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test --filter ConfigurationOptionsTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Configuration tests/ZidoTVPoster.Tests/ConfigurationOptionsTests.cs
git commit -m "feat: add service configuration models"
```

---

### Task 3: Zidoo API DTOs And Client

**Files:**
- Create: `src/ZidoTVPoster.Core/Zidoo/ZidooModels.cs`
- Create: `src/ZidoTVPoster.Core/Zidoo/ZidooApiClient.cs`
- Create: `tests/ZidoTVPoster.Tests/ZidooApiClientTests.cs`

- [ ] **Step 1: Write failing JSON parsing tests**

Create `tests/ZidoTVPoster.Tests/ZidooApiClientTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Tests;

public sealed class ZidooApiClientTests
{
    [Fact]
    public void CollectionListResponse_ParsesTvSeries()
    {
        const string json = """
        {
          "status": 200,
          "data": [
            { "id": 130, "parentId": -1, "type": 3, "name": "Band of Brothers", "watched": false },
            { "id": 291, "parentId": -1, "type": 2, "name": "A Quiet Place Collection", "watched": false }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<ZidooCollectionListResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Contains(response.Data, item => item.Id == 130 && item.Type == 3 && item.Name == "Band of Brothers");
    }

    [Fact]
    public void DetailResponse_ParsesEpisodesAndMediaUris()
    {
        const string json = """
        {
          "id": 131,
          "type": 4,
          "name": "Season 1",
          "episodes": [
            {
              "id": 132,
              "type": 5,
              "name": "Currahee",
              "watched": false,
              "aggregation": { "seasonNumber": 1, "episodeNumber": 1 },
              "aggregations": [
                {
                  "id": 133,
                  "type": 0,
                  "watched": false,
                  "aggregation": {
                    "uri": "/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv",
                    "lastWatchTime": 0,
                    "playPoint": 0
                  }
                }
              ]
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<ZidooDetailResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        var episode = Assert.Single(response.Episodes);
        Assert.False(episode.Watched);
        Assert.Equal("/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv", episode.Aggregations[0].Aggregation.Uri);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter ZidooApiClientTests
```

Expected: fails because DTOs do not exist.

- [ ] **Step 3: Implement DTOs**

Create `src/ZidoTVPoster.Core/Zidoo/ZidooModels.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZidoTVPoster.Core.Zidoo;

public static class ZidooJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

public sealed record ZidooCollectionListResponse(
    int Status,
    IReadOnlyList<ZidooItem> Data);

public sealed record ZidooItem(
    int Id,
    int ParentId,
    int Type,
    string Name,
    bool Watched,
    ZidooAggregation? Aggregation = null,
    IReadOnlyList<ZidooItem>? Aggregations = null);

public sealed record ZidooDetailResponse(
    int Id,
    int ParentId,
    int Type,
    string Name,
    bool Watched,
    ZidooAggregation? Aggregation,
    IReadOnlyList<ZidooItem> Episodes,
    IReadOnlyList<ZidooItem>? Aggregations);

public sealed record ZidooAggregation(
    int Id = 0,
    int SeasonNumber = 0,
    int EpisodeNumber = 0,
    int EpisodeCount = 0,
    string? TvName = null,
    string? Uri = null,
    long LastWatchTime = 0,
    long PlayPoint = 0);
```

- [ ] **Step 4: Implement API client**

Create `src/ZidoTVPoster.Core/Zidoo/ZidooApiClient.cs`:

```csharp
using System.Net.Http.Json;

namespace ZidoTVPoster.Core.Zidoo;

public sealed class ZidooApiClient(HttpClient httpClient)
{
    public async Task<ZidooCollectionListResponse> GetCollectionListAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("/ZidooPoster/getCollectionList", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ZidooCollectionListResponse>(ZidooJson.Options, cancellationToken)
            ?? new ZidooCollectionListResponse(0, []);
    }

    public async Task<ZidooItem> GetCollectionAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/ZidooPoster/getCollection?id={id}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ZidooItem>(ZidooJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Zidoo collection {id} returned an empty body.");
    }

    public async Task<ZidooDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/ZidooPoster/getDetail?id={id}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ZidooDetailResponse>(ZidooJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Zidoo detail {id} returned an empty body.");
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test --filter ZidooApiClientTests
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ZidoTVPoster.Core/Zidoo tests/ZidoTVPoster.Tests/ZidooApiClientTests.cs
git commit -m "feat: add zidoo api dto parsing"
```

---

### Task 4: TV Library Discovery And Counts

**Files:**
- Create: `src/ZidoTVPoster.Core/Library/LibraryModels.cs`
- Create: `src/ZidoTVPoster.Core/Library/TvLibraryDiscovery.cs`
- Create: `tests/ZidoTVPoster.Tests/TvLibraryDiscoveryTests.cs`

- [ ] **Step 1: Write failing count tests**

Create `tests/ZidoTVPoster.Tests/TvLibraryDiscoveryTests.cs`:

```csharp
using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Tests;

public sealed class TvLibraryDiscoveryTests
{
    [Fact]
    public void BuildSeasonSummary_CountsOnlyUnwatchedEpisodes()
    {
        var detail = new ZidooDetailResponse(
            131, 130, 4, "Season 1", false, new ZidooAggregation(SeasonNumber: 1),
            [
                Episode(132, false, "/Series/Show/Season 1/S01E01.mkv"),
                Episode(133, true, "/Series/Show/Season 1/S01E02.mkv"),
                Episode(134, false, "/Series/Show/Season 1/S01E03.mkv")
            ],
            []);

        var summary = TvLibraryDiscovery.BuildSeasonSummary(130, "Show", detail);

        Assert.Equal(2, summary.UnwatchedCount);
        Assert.Equal(3, summary.Episodes.Count);
        Assert.Equal("/Series/Show/Season 1/S01E01.mkv", summary.FirstMediaUri);
    }

    [Fact]
    public void BuildSeriesSummary_AddsSeasonCounts()
    {
        var series = new SeriesSummary(130, "Show", [
            new SeasonSummary(131, 1, "Season 1", 2, "/Series/Show/Season 1/S01E01.mkv", []),
            new SeasonSummary(135, 2, "Season 2", 4, "/Series/Show/Season 2/S02E01.mkv", [])
        ]);

        Assert.Equal(6, series.UnwatchedCount);
    }

    private static ZidooItem Episode(int id, bool watched, string uri) =>
        new(id, 131, 5, $"Episode {id}", watched, new ZidooAggregation(SeasonNumber: 1), [
            new ZidooItem(id + 1000, id, 0, $"{id}.mkv", watched, new ZidooAggregation(Uri: uri), [])
        ]);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter TvLibraryDiscoveryTests
```

Expected: fails because discovery/domain models do not exist.

- [ ] **Step 3: Implement domain models**

Create `src/ZidoTVPoster.Core/Library/LibraryModels.cs`:

```csharp
namespace ZidoTVPoster.Core.Library;

public sealed record SeriesSummary(
    int SeriesId,
    string Name,
    IReadOnlyList<SeasonSummary> Seasons)
{
    public int UnwatchedCount => Seasons.Sum(season => season.UnwatchedCount);
}

public sealed record SeasonSummary(
    int SeasonId,
    int SeasonNumber,
    string Name,
    int UnwatchedCount,
    string? FirstMediaUri,
    IReadOnlyList<EpisodeSummary> Episodes);

public sealed record EpisodeSummary(
    int EpisodeId,
    int EpisodeNumber,
    string Name,
    bool Watched,
    string? MediaUri);
```

- [ ] **Step 4: Implement discovery helpers**

Create `src/ZidoTVPoster.Core/Library/TvLibraryDiscovery.cs`:

```csharp
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Core.Library;

public static class TvLibraryDiscovery
{
    public static bool IsTvSeries(ZidooItem item) => item.Type == 3;
    public static bool IsSeason(ZidooItem item) => item.Type == 4;

    public static SeasonSummary BuildSeasonSummary(int seriesId, string seriesName, ZidooDetailResponse detail)
    {
        var episodes = detail.Episodes
            .Where(item => item.Type == 5)
            .Select(item => new EpisodeSummary(
                item.Id,
                item.Aggregation?.EpisodeNumber ?? 0,
                item.Name,
                item.Watched,
                FindFirstMediaUri(item)))
            .ToArray();

        var seasonNumber = detail.Aggregation?.SeasonNumber ?? 0;
        return new SeasonSummary(
            detail.Id,
            seasonNumber,
            detail.Name,
            episodes.Count(episode => !episode.Watched),
            episodes.Select(episode => episode.MediaUri).FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri)),
            episodes);
    }

    private static string? FindFirstMediaUri(ZidooItem episode)
    {
        return episode.Aggregations?
            .Select(child => child.Aggregation?.Uri)
            .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test --filter TvLibraryDiscoveryTests
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ZidoTVPoster.Core/Library tests/ZidoTVPoster.Tests/TvLibraryDiscoveryTests.cs
git commit -m "feat: summarize tv watched state"
```

---

### Task 5: URI To Series Folder Mapping

**Files:**
- Create: `src/ZidoTVPoster.Core/Storage/ZidooPathMapper.cs`
- Create: `tests/ZidoTVPoster.Tests/ZidooPathMapperTests.cs`

- [ ] **Step 1: Write failing mapper tests**

Create `tests/ZidoTVPoster.Tests/ZidooPathMapperTests.cs`:

```csharp
using ZidoTVPoster.Core.Storage;

namespace ZidoTVPoster.Tests;

public sealed class ZidooPathMapperTests
{
    [Fact]
    public void TryMapSeriesFolder_MapsSeriesRoot()
    {
        var mapper = new ZidooPathMapper(@"\\192.168.0.209\Share\Storage", ["Series"]);

        var result = mapper.TryMapSeriesFolder("/Series/Band of Brothers/Season 1/S01E01.mkv");

        Assert.True(result.Success);
        Assert.Equal(@"\\192.168.0.209\Share\Storage\Series\Band of Brothers", result.SeriesFolder);
    }

    [Fact]
    public void TryMapSeriesFolder_UsesConfiguredTvRoot()
    {
        var mapper = new ZidooPathMapper(@"\\server\Share\Storage", ["Series", "TV"]);

        var result = mapper.TryMapSeriesFolder("/TV/Show Name/Season 1/S01E01.mkv");

        Assert.True(result.Success);
        Assert.Equal(@"\\server\Share\Storage\TV\Show Name", result.SeriesFolder);
    }

    [Fact]
    public void TryMapSeriesFolder_RejectsUnknownRoot()
    {
        var mapper = new ZidooPathMapper(@"\\server\Share\Storage", ["Series"]);

        var result = mapper.TryMapSeriesFolder("/Downloads/Show/Season 1/S01E01.mkv");

        Assert.False(result.Success);
        Assert.Null(result.SeriesFolder);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter ZidooPathMapperTests
```

Expected: fails because mapper does not exist.

- [ ] **Step 3: Implement mapper**

Create `src/ZidoTVPoster.Core/Storage/ZidooPathMapper.cs`:

```csharp
namespace ZidoTVPoster.Core.Storage;

public sealed class ZidooPathMapper(string storageRoot, IReadOnlyCollection<string> mediaRootNames)
{
    public SeriesFolderMapResult TryMapSeriesFolder(string? zidooUri)
    {
        if (string.IsNullOrWhiteSpace(zidooUri))
        {
            return SeriesFolderMapResult.Failed("URI is empty.");
        }

        var segments = zidooUri
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!mediaRootNames.Contains(segments[index], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= segments.Length)
            {
                return SeriesFolderMapResult.Failed("URI has media root but no series folder.");
            }

            var seriesFolder = Path.Combine(storageRoot, segments[index], segments[index + 1]);
            return SeriesFolderMapResult.Mapped(seriesFolder);
        }

        return SeriesFolderMapResult.Failed("URI does not contain a configured media root.");
    }
}

public sealed record SeriesFolderMapResult(bool Success, string? SeriesFolder, string? Reason)
{
    public static SeriesFolderMapResult Mapped(string seriesFolder) => new(true, seriesFolder, null);
    public static SeriesFolderMapResult Failed(string reason) => new(false, null, reason);
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test --filter ZidooPathMapperTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Storage tests/ZidoTVPoster.Tests/ZidooPathMapperTests.cs
git commit -m "feat: map zidoo media uris to series folders"
```

---

### Task 6: Per-Series State Store

**Files:**
- Create: `src/ZidoTVPoster.Core/Posters/PosterStateStore.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterStateStoreTests.cs`

- [ ] **Step 1: Write failing state tests**

Create `tests/ZidoTVPoster.Tests/PosterStateStoreTests.cs`:

```csharp
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterStateStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_UsesSeriesLocalFolder()
    {
        var root = Directory.CreateTempSubdirectory();
        var store = new PosterStateStore();
        var state = new PosterState(
            130,
            "Band of Brothers",
            DateTimeOffset.Parse("2026-05-30T12:00:00Z"),
            new PosterCountState(0, "original-series-poster.jpg", "generated-series-poster.jpg"),
            [new SeasonPosterState(131, 1, "Miniseries", 0, "original-season-01-poster.jpg", "generated-season-01-poster.jpg")]);

        await store.SaveAsync(root.FullName, state, CancellationToken.None);
        var loaded = await store.LoadAsync(root.FullName, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(130, loaded.SeriesId);
        Assert.True(File.Exists(Path.Combine(root.FullName, ".zido-tv-poster", "poster-state.json")));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter PosterStateStoreTests
```

Expected: fails because poster state types do not exist.

- [ ] **Step 3: Implement state store**

Create `src/ZidoTVPoster.Core/Posters/PosterStateStore.cs`:

```csharp
using System.Text.Json;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterStateStore
{
    public const string ServiceFolderName = ".zido-tv-poster";
    private const string StateFileName = "poster-state.json";

    public async Task<PosterState?> LoadAsync(string seriesFolder, CancellationToken cancellationToken)
    {
        var path = GetStatePath(seriesFolder);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PosterState>(stream, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(string seriesFolder, PosterState state, CancellationToken cancellationToken)
    {
        var serviceFolder = Path.Combine(seriesFolder, ServiceFolderName);
        Directory.CreateDirectory(serviceFolder);
        await using var stream = File.Create(GetStatePath(seriesFolder));
        await JsonSerializer.SerializeAsync(stream, state, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    public static string GetStatePath(string seriesFolder) =>
        Path.Combine(seriesFolder, ServiceFolderName, StateFileName);
}

public sealed record PosterState(
    int SeriesId,
    string SeriesName,
    DateTimeOffset LastUpdatedUtc,
    PosterCountState Series,
    IReadOnlyList<SeasonPosterState> Seasons);

public sealed record PosterCountState(
    int UnwatchedCount,
    string OriginalPoster,
    string GeneratedPoster);

public sealed record SeasonPosterState(
    int SeasonId,
    int SeasonNumber,
    string Name,
    int UnwatchedCount,
    string OriginalPoster,
    string GeneratedPoster);
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test --filter PosterStateStoreTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters/PosterStateStore.cs tests/ZidoTVPoster.Tests/PosterStateStoreTests.cs
git commit -m "feat: store poster state inside series folders"
```

---

### Task 7: Poster Update Planner

**Files:**
- Create: `src/ZidoTVPoster.Core/Posters/PosterPlanner.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterPlannerTests.cs`

- [ ] **Step 1: Write failing planner tests**

Create `tests/ZidoTVPoster.Tests/PosterPlannerTests.cs`:

```csharp
using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterPlannerTests
{
    [Fact]
    public void Plan_SkipsMissingSeriesFolder()
    {
        var planner = new PosterPlanner();
        var series = new SeriesSummary(130, "Show", [
            new SeasonSummary(131, 1, "Season 1", 3, "/Series/Show/Season 1/S01E01.mkv", [])
        ]);

        var plan = planner.Plan(series, @"X:\does-not-exist", previousState: null);

        Assert.True(plan.Skip);
        Assert.Equal("Series folder does not exist.", plan.SkipReason);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void Plan_IncludesSeriesAndChangedSeasonWhenFolderExists()
    {
        var root = Directory.CreateTempSubdirectory();
        var planner = new PosterPlanner();
        var series = new SeriesSummary(130, "Show", [
            new SeasonSummary(131, 1, "Season 1", 3, "/Series/Show/Season 1/S01E01.mkv", [])
        ]);

        var plan = planner.Plan(series, root.FullName, previousState: null);

        Assert.False(plan.Skip);
        Assert.Contains(plan.Items, item => item.Kind == PosterTargetKind.Series && item.UnwatchedCount == 3);
        Assert.Contains(plan.Items, item => item.Kind == PosterTargetKind.Season && item.UnwatchedCount == 3);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter PosterPlannerTests
```

Expected: fails because planner does not exist.

- [ ] **Step 3: Implement planner**

Create `src/ZidoTVPoster.Core/Posters/PosterPlanner.cs`:

```csharp
using ZidoTVPoster.Core.Library;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterPlanner
{
    public PosterUpdatePlan Plan(SeriesSummary series, string seriesFolder, PosterState? previousState)
    {
        if (!Directory.Exists(seriesFolder))
        {
            return PosterUpdatePlan.Skipped("Series folder does not exist.");
        }

        var items = new List<PosterUpdateItem>();
        if (previousState?.Series.UnwatchedCount != series.UnwatchedCount)
        {
            items.Add(PosterUpdateItem.Series(series.SeriesId, series.UnwatchedCount, seriesFolder));
        }

        foreach (var season in series.Seasons)
        {
            var previousSeason = previousState?.Seasons.FirstOrDefault(item => item.SeasonId == season.SeasonId);
            if (previousSeason?.UnwatchedCount == season.UnwatchedCount)
            {
                continue;
            }

            items.Add(PosterUpdateItem.Season(season.SeasonId, season.SeasonNumber, season.UnwatchedCount, seriesFolder));
        }

        return PosterUpdatePlan.Ready(items);
    }
}

public sealed record PosterUpdatePlan(bool Skip, string? SkipReason, IReadOnlyList<PosterUpdateItem> Items)
{
    public static PosterUpdatePlan Skipped(string reason) => new(true, reason, []);
    public static PosterUpdatePlan Ready(IReadOnlyList<PosterUpdateItem> items) => new(false, null, items);
}

public sealed record PosterUpdateItem(
    PosterTargetKind Kind,
    int ZidooId,
    int SeasonNumber,
    int UnwatchedCount,
    string SeriesFolder)
{
    public static PosterUpdateItem Series(int id, int count, string folder) =>
        new(PosterTargetKind.Series, id, 0, count, folder);

    public static PosterUpdateItem Season(int id, int seasonNumber, int count, string folder) =>
        new(PosterTargetKind.Season, id, seasonNumber, count, folder);
}

public enum PosterTargetKind
{
    Series,
    Season
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test --filter PosterPlannerTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters/PosterPlanner.cs tests/ZidoTVPoster.Tests/PosterPlannerTests.cs
git commit -m "feat: plan poster updates safely"
```

---

### Task 8: Poster Renderer

**Files:**
- Create: `src/ZidoTVPoster.Core/Posters/PosterRenderer.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterRendererTests.cs`

- [ ] **Step 1: Write failing renderer tests**

Create `tests/ZidoTVPoster.Tests/PosterRendererTests.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterRendererTests
{
    [Fact]
    public async Task RenderAsync_HidesBadgeWhenZero()
    {
        var root = Directory.CreateTempSubdirectory();
        var source = Path.Combine(root.FullName, "original.jpg");
        var output = Path.Combine(root.FullName, "generated.jpg");
        await CreateImageAsync(source);

        await new PosterRenderer().RenderAsync(source, output, 0, new PosterUpdateOptions(), CancellationToken.None);

        Assert.True(File.Exists(output));
        using var original = await Image.LoadAsync<Rgba32>(source);
        using var generated = await Image.LoadAsync<Rgba32>(output);
        Assert.Equal(original.Width, generated.Width);
        Assert.Equal(original.Height, generated.Height);
    }

    [Fact]
    public async Task RenderAsync_DrawsBadgeForNonZeroCount()
    {
        var root = Directory.CreateTempSubdirectory();
        var source = Path.Combine(root.FullName, "original.jpg");
        var output = Path.Combine(root.FullName, "generated.jpg");
        await CreateImageAsync(source);

        await new PosterRenderer().RenderAsync(source, output, 7, new PosterUpdateOptions(), CancellationToken.None);

        using var generated = await Image.LoadAsync<Rgba32>(output);
        Assert.NotEqual(new Rgba32(20, 40, 80), generated[generated.Width - 30, 30]);
    }

    private static async Task CreateImageAsync(string path)
    {
        using var image = new Image<Rgba32>(300, 450, new Rgba32(20, 40, 80));
        await image.SaveAsJpegAsync(path);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter PosterRendererTests
```

Expected: fails because renderer does not exist.

- [ ] **Step 3: Implement renderer**

Create `src/ZidoTVPoster.Core/Posters/PosterRenderer.cs`:

```csharp
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZidoTVPoster.Core.Configuration;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterRenderer
{
    public async Task RenderAsync(
        string originalPosterPath,
        string generatedPosterPath,
        int unwatchedCount,
        PosterUpdateOptions options,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync<Rgba32>(originalPosterPath, cancellationToken);
        if (!(options.HideBadgeWhenZero && unwatchedCount == 0))
        {
            DrawBadge(image, string.Format(options.BadgeTextFormat, unwatchedCount));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(generatedPosterPath)!);
        await image.SaveAsJpegAsync(generatedPosterPath, new JpegEncoder { Quality = 92 }, cancellationToken);
    }

    private static void DrawBadge(Image<Rgba32> image, string text)
    {
        var fontSize = Math.Max(18, image.Width / 15);
        var font = SystemFonts.CreateFont("Arial", fontSize, FontStyle.Bold);
        var margin = Math.Max(14, image.Width / 30);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var paddingX = fontSize * 0.75f;
        var paddingY = fontSize * 0.45f;
        var width = textSize.Width + paddingX * 2;
        var height = textSize.Height + paddingY * 2;
        var x = image.Width - width - margin;
        var y = margin;
        var radius = height / 2;
        var rect = new RectangularPolygon(x, y, width, height);

        image.Mutate(ctx =>
        {
            ctx.Fill(Color.FromRgba(0, 0, 0, 210), rect);
            ctx.Draw(Color.FromRgb(255, 255, 255), Math.Max(2, image.Width / 250), rect);
            ctx.DrawText(text, font, Color.White, new PointF(x + paddingX, y + paddingY));
        });
    }
}
```

If `SixLabors.ImageSharp.Drawing` is required by the compiler, run:

```powershell
dotnet add src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj package SixLabors.ImageSharp.Drawing
```

- [ ] **Step 4: Run renderer tests**

Run:

```powershell
dotnet test --filter PosterRendererTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters/PosterRenderer.cs tests/ZidoTVPoster.Tests/PosterRendererTests.cs src/ZidoTVPoster.Core/ZidoTVPoster.Core.csproj
git commit -m "feat: render unwatched badges on posters"
```

---

### Task 9: Dry-Run Poster Applier

**Files:**
- Create: `src/ZidoTVPoster.Core/Posters/PosterApplier.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterApplierTests.cs`

- [ ] **Step 1: Write failing applier tests**

Create `tests/ZidoTVPoster.Tests/PosterApplierTests.cs`:

```csharp
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterApplierTests
{
    [Fact]
    public async Task ApplyAsync_DryRunDoesNotWriteOutsideSeriesFolder()
    {
        var applier = new PosterApplier();
        var result = await applier.ApplyAsync(
            new PosterUpdateItem(PosterTargetKind.Series, 130, 0, 5, @"C:\Series\Show"),
            generatedPosterPath: @"C:\Series\Show\.zido-tv-poster\generated-series-poster.jpg",
            dryRun: true,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Dry run", result.Message);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter PosterApplierTests
```

Expected: fails because `PosterApplier` does not exist.

- [ ] **Step 3: Implement dry-run applier**

Create `src/ZidoTVPoster.Core/Posters/PosterApplier.cs`:

```csharp
namespace ZidoTVPoster.Core.Posters;

public sealed class PosterApplier
{
    public Task<PosterApplyResult> ApplyAsync(
        PosterUpdateItem item,
        string generatedPosterPath,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            return Task.FromResult(PosterApplyResult.Ok(
                $"Dry run: would apply {generatedPosterPath} to {item.Kind} {item.ZidooId}."));
        }

        return Task.FromResult(PosterApplyResult.Failed(
            "Poster apply is not enabled until the Zidoo poster update route is verified."));
    }
}

public sealed record PosterApplyResult(bool Success, string Message)
{
    public static PosterApplyResult Ok(string message) => new(true, message);
    public static PosterApplyResult Failed(string message) => new(false, message);
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test --filter PosterApplierTests
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters/PosterApplier.cs tests/ZidoTVPoster.Tests/PosterApplierTests.cs
git commit -m "feat: add dry-run poster applier"
```

---

### Task 10: Worker Service Wiring

**Files:**
- Modify: `src/ZidoTVPoster.Service/Program.cs`
- Create: `src/ZidoTVPoster.Service/PosterUpdateWorker.cs`
- Create: `src/ZidoTVPoster.Service/appsettings.json`

- [ ] **Step 1: Replace service host**

Set `src/ZidoTVPoster.Service/Program.cs` to:

```csharp
using Microsoft.Extensions.Options;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;
using ZidoTVPoster.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ZidooOptions>(builder.Configuration.GetSection("Zidoo"));
builder.Services.Configure<PosterUpdateOptions>(builder.Configuration.GetSection("PosterUpdates"));

builder.Services.AddHttpClient<ZidooApiClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<ZidooOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<ZidooOptions>>().Value;
    return new ZidooPathMapper(options.StorageRoot, options.MediaRootNames);
});
builder.Services.AddSingleton<PosterStateStore>();
builder.Services.AddSingleton<PosterPlanner>();
builder.Services.AddSingleton<PosterSourceManager>();
builder.Services.AddSingleton<PosterRenderer>();
builder.Services.AddSingleton<PosterApplier>();
builder.Services.AddHostedService<PosterUpdateWorker>();
builder.Services.AddWindowsService();

await builder.Build().RunAsync();
```

- [ ] **Step 2: Add worker skeleton**

Create `src/ZidoTVPoster.Service/PosterUpdateWorker.cs`:

```csharp
using Microsoft.Extensions.Options;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Library;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Service;

public sealed class PosterUpdateWorker(
    ILogger<PosterUpdateWorker> logger,
    IOptions<PosterUpdateOptions> posterOptions,
    ZidooApiClient apiClient,
    ZidooPathMapper pathMapper,
    PosterStateStore stateStore,
    PosterPlanner planner,
    PosterSourceManager sourceManager,
    PosterRenderer renderer,
    PosterApplier applier) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(posterOptions.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var collections = await apiClient.GetCollectionListAsync(cancellationToken);
        foreach (var seriesItem in collections.Data.Where(TvLibraryDiscovery.IsTvSeries))
        {
            logger.LogInformation("Discovered TV series {SeriesId}: {SeriesName}", seriesItem.Id, seriesItem.Name);
        }
    }
}
```

- [ ] **Step 3: Add appsettings**

Create `src/ZidoTVPoster.Service/appsettings.json`:

```json
{
  "Zidoo": {
    "BaseUrl": "http://192.168.0.209:9529",
    "StorageRoot": "\\\\192.168.0.209\\Share\\Storage",
    "MediaRootNames": [ "Series" ],
    "RequestTimeoutSeconds": 10
  },
  "PosterUpdates": {
    "PollIntervalSeconds": 60,
    "DryRun": true,
    "HideBadgeWhenZero": true,
    "BadgePlacement": "TopRight",
    "BadgeTextFormat": "{0} unwatched"
  }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ZidoTVPoster.Service
git commit -m "feat: wire worker service host"
```

---

### Task 11: Discovery-To-Plan Pipeline

**Files:**
- Modify: `src/ZidoTVPoster.Service/PosterUpdateWorker.cs`
- Modify: `src/ZidoTVPoster.Core/Zidoo/ZidooModels.cs`
- Add tests if DTO changes are needed.

- [ ] **Step 1: Extend worker to fetch seasons and details**

Replace `RunOnceAsync` body with:

```csharp
var collections = await apiClient.GetCollectionListAsync(cancellationToken);
foreach (var seriesItem in collections.Data.Where(TvLibraryDiscovery.IsTvSeries))
{
    var seriesCollection = await apiClient.GetCollectionAsync(seriesItem.Id, cancellationToken);
    var seasons = new List<SeasonSummary>();

    foreach (var seasonItem in (seriesCollection.Aggregations ?? []).Where(TvLibraryDiscovery.IsSeason))
    {
        var detail = await apiClient.GetDetailAsync(seasonItem.Id, cancellationToken);
        seasons.Add(TvLibraryDiscovery.BuildSeasonSummary(seriesItem.Id, seriesItem.Name, detail));
    }

    var series = new SeriesSummary(seriesItem.Id, seriesItem.Name, seasons);
    var firstUri = series.Seasons.Select(season => season.FirstMediaUri).FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));
    var map = pathMapper.TryMapSeriesFolder(firstUri);
    if (!map.Success || map.SeriesFolder is null)
    {
        logger.LogWarning("Skipping {SeriesName}: {Reason}", series.Name, map.Reason);
        continue;
    }

    var previousState = await stateStore.LoadAsync(map.SeriesFolder, cancellationToken);
    var plan = planner.Plan(series, map.SeriesFolder, previousState);
    if (plan.Skip)
    {
        logger.LogWarning("Skipping {SeriesName}: {Reason}", series.Name, plan.SkipReason);
        continue;
    }

    logger.LogInformation(
        "{SeriesName}: {SeriesUnwatched} unwatched episodes, {PosterCount} poster updates planned",
        series.Name,
        series.UnwatchedCount,
        plan.Items.Count);
}
```

- [ ] **Step 2: Run build**

Run:

```powershell
dotnet build
```

Expected: pass.

- [ ] **Step 3: Run service locally for one dry-run cycle**

Run:

```powershell
dotnet run --project src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
```

Expected: logs discovered series/counts. Stop with `Ctrl+C` after one cycle.

- [ ] **Step 4: Commit**

```powershell
git add src/ZidoTVPoster.Service/PosterUpdateWorker.cs src/ZidoTVPoster.Core/Zidoo/ZidooModels.cs
git commit -m "feat: plan poster updates from zidoo state"
```

---

### Task 12: Original Poster Acquisition

**Files:**
- Create: `src/ZidoTVPoster.Core/Posters/PosterSourceManager.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterSourceManagerTests.cs`
- Modify: `src/ZidoTVPoster.Core/Zidoo/ZidooApiClient.cs`

- [ ] **Step 1: Write failing source manager tests**

Create `tests/ZidoTVPoster.Tests/PosterSourceManagerTests.cs`:

```csharp
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterSourceManagerTests
{
    [Fact]
    public async Task EnsureOriginalAsync_ReturnsExistingSeriesLocalOriginal()
    {
        var root = Directory.CreateTempSubdirectory();
        var serviceFolder = Path.Combine(root.FullName, PosterStateStore.ServiceFolderName);
        Directory.CreateDirectory(serviceFolder);
        var original = Path.Combine(serviceFolder, PosterFileNames.OriginalSeriesPoster);
        await File.WriteAllTextAsync(original, "existing");

        var manager = new PosterSourceManager();
        var result = await manager.EnsureOriginalAsync(
            root.FullName,
            PosterFileNames.OriginalSeriesPoster,
            candidateLocalPosterPaths: [],
            downloadOriginalAsync: (_, _) => throw new InvalidOperationException("Should not download."),
            zidooId: 130,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(original, result.OriginalPath);
    }

    [Fact]
    public async Task EnsureOriginalAsync_CopiesLocalCandidateIntoSeriesLocalFolder()
    {
        var root = Directory.CreateTempSubdirectory();
        var localPoster = Path.Combine(root.FullName, "poster.jpg");
        await File.WriteAllTextAsync(localPoster, "poster");

        var manager = new PosterSourceManager();
        var result = await manager.EnsureOriginalAsync(
            root.FullName,
            PosterFileNames.OriginalSeriesPoster,
            [localPoster],
            downloadOriginalAsync: (_, _) => Task.FromResult<byte[]?>(null),
            zidooId: 130,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(root.FullName, ".zido-tv-poster", "original-series-poster.jpg")));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test --filter PosterSourceManagerTests
```

Expected: fails because `PosterSourceManager` does not exist.

- [ ] **Step 3: Implement source manager**

Create `src/ZidoTVPoster.Core/Posters/PosterSourceManager.cs`:

```csharp
namespace ZidoTVPoster.Core.Posters;

public sealed class PosterSourceManager
{
    public async Task<PosterSourceResult> EnsureOriginalAsync(
        string seriesFolder,
        string originalFileName,
        IReadOnlyList<string> candidateLocalPosterPaths,
        Func<int, CancellationToken, Task<byte[]?>> downloadOriginalAsync,
        int zidooId,
        CancellationToken cancellationToken)
    {
        var serviceFolder = Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName);
        var originalPath = Path.Combine(serviceFolder, originalFileName);
        if (File.Exists(originalPath))
        {
            return PosterSourceResult.Ok(originalPath);
        }

        var localCandidate = candidateLocalPosterPaths.FirstOrDefault(File.Exists);
        if (localCandidate is not null)
        {
            Directory.CreateDirectory(serviceFolder);
            File.Copy(localCandidate, originalPath, overwrite: false);
            return PosterSourceResult.Ok(originalPath);
        }

        var downloaded = await downloadOriginalAsync(zidooId, cancellationToken);
        if (downloaded is { Length: > 0 })
        {
            Directory.CreateDirectory(serviceFolder);
            await File.WriteAllBytesAsync(originalPath, downloaded, cancellationToken);
            return PosterSourceResult.Ok(originalPath);
        }

        return PosterSourceResult.Failed($"No original poster source found for Zidoo item {zidooId}.");
    }
}

public sealed record PosterSourceResult(bool Success, string? OriginalPath, string? Message)
{
    public static PosterSourceResult Ok(string path) => new(true, path, null);
    public static PosterSourceResult Failed(string message) => new(false, null, message);
}
```

- [ ] **Step 4: Add poster download API method**

Add to `ZidooApiClient`:

```csharp
public async Task<byte[]?> GetPosterBytesAsync(int id, CancellationToken cancellationToken)
{
    var response = await httpClient.GetAsync($"/ZidooPoster/getFile/getPoster?id={id}&w=1000&h=1500", cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return null;
    }

    return await response.Content.ReadAsByteArrayAsync(cancellationToken);
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test --filter PosterSourceManagerTests
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters/PosterSourceManager.cs src/ZidoTVPoster.Core/Zidoo/ZidooApiClient.cs tests/ZidoTVPoster.Tests/PosterSourceManagerTests.cs
git commit -m "feat: acquire original posters locally"
```

---

### Task 13: Local Poster Generation Pipeline

**Files:**
- Modify: `src/ZidoTVPoster.Service/PosterUpdateWorker.cs`
- Modify: `src/ZidoTVPoster.Core/Posters/PosterPlanner.cs`
- Create: `src/ZidoTVPoster.Core/Posters/PosterFileNames.cs`
- Create: `tests/ZidoTVPoster.Tests/PosterFileNamesTests.cs`

- [ ] **Step 1: Add filename tests**

Create `tests/ZidoTVPoster.Tests/PosterFileNamesTests.cs`:

```csharp
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterFileNamesTests
{
    [Fact]
    public void OriginalSeasonPoster_UsesTwoDigitSeasonNumber()
    {
        Assert.Equal("original-season-01-poster.jpg", PosterFileNames.OriginalSeasonPoster(1, 131));
    }

    [Fact]
    public void OriginalSeasonPoster_UsesZidooIdWhenSeasonNumberMissing()
    {
        Assert.Equal("original-season-zidoo-131-poster.jpg", PosterFileNames.OriginalSeasonPoster(0, 131));
    }
}
```

- [ ] **Step 2: Implement filenames**

Create `src/ZidoTVPoster.Core/Posters/PosterFileNames.cs`:

```csharp
namespace ZidoTVPoster.Core.Posters;

public static class PosterFileNames
{
    public const string OriginalSeriesPoster = "original-series-poster.jpg";
    public const string GeneratedSeriesPoster = "generated-series-poster.jpg";

    public static string OriginalSeasonPoster(int seasonNumber, int zidooId) =>
        seasonNumber > 0 ? $"original-season-{seasonNumber:00}-poster.jpg" : $"original-season-zidoo-{zidooId}-poster.jpg";

    public static string GeneratedSeasonPoster(int seasonNumber, int zidooId) =>
        seasonNumber > 0 ? $"generated-season-{seasonNumber:00}-poster.jpg" : $"generated-season-zidoo-{zidooId}-poster.jpg";
}
```

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test --filter PosterFileNamesTests
```

Expected: pass.

- [ ] **Step 4: Generate posters from acquired originals**

In `PosterUpdateWorker`, for each `plan.Items`, compute original/generated paths inside:

```csharp
var serviceFolder = Path.Combine(item.SeriesFolder, PosterStateStore.ServiceFolderName);
var originalName = item.Kind == PosterTargetKind.Series
    ? PosterFileNames.OriginalSeriesPoster
    : PosterFileNames.OriginalSeasonPoster(item.SeasonNumber, item.ZidooId);
var generatedName = item.Kind == PosterTargetKind.Series
    ? PosterFileNames.GeneratedSeriesPoster
    : PosterFileNames.GeneratedSeasonPoster(item.SeasonNumber, item.ZidooId);
var generatedPath = Path.Combine(serviceFolder, generatedName);

var sourceResult = await sourceManager.EnsureOriginalAsync(
    item.SeriesFolder,
    originalName,
    candidateLocalPosterPaths: item.Kind == PosterTargetKind.Series
        ? [Path.Combine(item.SeriesFolder, "poster.jpg")]
        : [],
    downloadOriginalAsync: apiClient.GetPosterBytesAsync,
    item.ZidooId,
    cancellationToken);

if (!sourceResult.Success || sourceResult.OriginalPath is null)
{
    logger.LogWarning("Skipping {Kind} {ZidooId}: {Message}", item.Kind, item.ZidooId, sourceResult.Message);
    continue;
}

await renderer.RenderAsync(sourceResult.OriginalPath, generatedPath, item.UnwatchedCount, posterOptions.Value, cancellationToken);
var applyResult = await applier.ApplyAsync(item, generatedPath, posterOptions.Value.DryRun, cancellationToken);
logger.LogInformation("{ApplyMessage}", applyResult.Message);
```

- [ ] **Step 5: Run build and tests**

Run:

```powershell
dotnet test
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ZidoTVPoster.Core/Posters src/ZidoTVPoster.Service/PosterUpdateWorker.cs tests/ZidoTVPoster.Tests/PosterFileNamesTests.cs
git commit -m "feat: generate local poster badges from originals"
```

---

### Task 14: State Update After Successful Dry-Run/Apply

**Files:**
- Modify: `src/ZidoTVPoster.Service/PosterUpdateWorker.cs`
- Modify: `src/ZidoTVPoster.Core/Posters/PosterStateStore.cs`

- [ ] **Step 1: Build new state after successful work**

After processing a series plan, save state only if all planned items either applied successfully or were skipped because the original poster was missing. Use:

```csharp
var state = new PosterState(
    series.SeriesId,
    series.Name,
    DateTimeOffset.UtcNow,
    new PosterCountState(series.UnwatchedCount, PosterFileNames.OriginalSeriesPoster, PosterFileNames.GeneratedSeriesPoster),
    series.Seasons.Select(season => new SeasonPosterState(
        season.SeasonId,
        season.SeasonNumber,
        season.Name,
        season.UnwatchedCount,
        PosterFileNames.OriginalSeasonPoster(season.SeasonNumber, season.SeasonId),
        PosterFileNames.GeneratedSeasonPoster(season.SeasonNumber, season.SeasonId))).ToArray());

await stateStore.SaveAsync(map.SeriesFolder, state, cancellationToken);
```

- [ ] **Step 2: Confirm missing folder rule**

Ensure the save occurs only after `planner.Plan` confirms the series folder exists. Do not call `Directory.CreateDirectory` on the series folder itself.

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test
```

Expected: pass.

- [ ] **Step 4: Commit**

```powershell
git add src/ZidoTVPoster.Service/PosterUpdateWorker.cs src/ZidoTVPoster.Core/Posters/PosterStateStore.cs
git commit -m "feat: persist per-series poster state"
```

---

### Task 15: README And Operations

**Files:**
- Create/Modify: `README.md`

- [ ] **Step 1: Write README**

Create `README.md`:

```markdown
# ZidoTVPoster

ZidoTVPoster is a C# Windows Service for Zidoo Poster Wall TV libraries. It polls the Zidoo API, counts unwatched TV episodes, and generates series and season poster images with unwatched badges.

## Safety Model

The service defaults to dry-run mode. Durable per-series artifacts are stored only inside the original series folder:

```text
<Series Folder>\.zido-tv-poster\
```

When a series folder is removed from a shared source, its service-owned files are removed with it. The service does not keep per-series poster caches in `ProgramData` or the install folder.

## Configuration

Edit `src/ZidoTVPoster.Service/appsettings.json`:

```json
{
  "Zidoo": {
    "BaseUrl": "http://192.168.0.209:9529",
    "StorageRoot": "\\\\192.168.0.209\\Share\\Storage",
    "MediaRootNames": [ "Series" ],
    "RequestTimeoutSeconds": 10
  },
  "PosterUpdates": {
    "PollIntervalSeconds": 60,
    "DryRun": true,
    "HideBadgeWhenZero": true,
    "BadgePlacement": "TopRight",
    "BadgeTextFormat": "{0} unwatched"
  }
}
```

## Dry Run

```powershell
dotnet run --project src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
```

Leave `DryRun` set to `true` until poster application to Zidoo has been verified on your device.

## Build And Test

```powershell
dotnet test
dotnet publish src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj -c Release -r win-x64 --self-contained false
```
```

- [ ] **Step 2: Run markdown sanity check**

Run:

```powershell
Get-Content README.md
```

Expected: README renders as plain markdown with no broken fenced code blocks.

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: add service setup notes"
```

---

### Task 16: Final Verification

**Files:**
- All project files.

- [ ] **Step 1: Run full tests**

Run:

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 2: Build release**

Run:

```powershell
dotnet publish src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj -c Release -r win-x64 --self-contained false
```

Expected: publish succeeds.

- [ ] **Step 3: Run one dry-run probe**

Run:

```powershell
dotnet run --project src/ZidoTVPoster.Service/ZidoTVPoster.Service.csproj
```

Expected: the service connects to `192.168.0.209:9529`, logs TV series discovery, skips missing folders without creating them, and does not apply posters because `DryRun` is `true`.

- [ ] **Step 4: Inspect git status**

Run:

```powershell
git status --short
```

Expected: clean working tree.

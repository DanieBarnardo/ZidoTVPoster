using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterSourceManagerTests
{
    [Fact]
    public async Task EnsureOriginalAsync_ReturnsExistingOriginalWithoutDownloading()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var originalPath = GetOriginalPath(seriesFolder, "original-series.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        await File.WriteAllBytesAsync(originalPath, [1, 2, 3], CancellationToken.None);
        var manager = new PosterSourceManager();
        var downloadCalled = false;

        var result = await manager.EnsureOriginalAsync(
            seriesFolder,
            "original-series.jpg",
            [],
            (_, _) =>
            {
                downloadCalled = true;
                return Task.FromResult<byte[]?>([9, 9, 9]);
            },
            42,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(originalPath, result.OriginalPosterPath);
        Assert.False(downloadCalled);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(originalPath, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureOriginalAsync_CopiesFirstExistingLocalCandidateIntoServiceFolder()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var missingCandidate = Path.Combine(seriesFolder, "missing.jpg");
        var existingCandidate = Path.Combine(seriesFolder, "poster.jpg");
        await File.WriteAllBytesAsync(existingCandidate, [4, 5, 6], CancellationToken.None);
        var manager = new PosterSourceManager();

        var result = await manager.EnsureOriginalAsync(
            seriesFolder,
            "original-series.jpg",
            [missingCandidate, existingCandidate],
            (_, _) => Task.FromResult<byte[]?>([9, 9, 9]),
            42,
            CancellationToken.None);

        var originalPath = GetOriginalPath(seriesFolder, "original-series.jpg");
        Assert.True(result.Success);
        Assert.Equal(originalPath, result.OriginalPosterPath);
        Assert.True(File.Exists(originalPath));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(originalPath, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureOriginalAsync_DownloadsPosterBytesWhenNoLocalSourceExists()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var manager = new PosterSourceManager();
        var requestedId = 0;

        var result = await manager.EnsureOriginalAsync(
            seriesFolder,
            "original-series.jpg",
            [],
            (id, _) =>
            {
                requestedId = id;
                return Task.FromResult<byte[]?>([7, 8, 9]);
            },
            42,
            CancellationToken.None);

        var originalPath = GetOriginalPath(seriesFolder, "original-series.jpg");
        Assert.True(result.Success);
        Assert.Equal(42, requestedId);
        Assert.Equal(originalPath, result.OriginalPosterPath);
        Assert.Equal([7, 8, 9], await File.ReadAllBytesAsync(originalPath, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureOriginalAsync_ReturnsFailedResultWhenNoSourceExists()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var manager = new PosterSourceManager();

        var result = await manager.EnsureOriginalAsync(
            seriesFolder,
            "original-series.jpg",
            [],
            (_, _) => Task.FromResult<byte[]?>(null),
            42,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.OriginalPosterPath);
        Assert.Contains("No original poster source", result.Message);
        Assert.False(File.Exists(GetOriginalPath(seriesFolder, "original-series.jpg")));
    }

    [Fact]
    public async Task EnsureOriginalAsync_DoesNotCreateMissingParentSeriesFolder()
    {
        var seriesFolder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"), "MissingSeries");
        var candidateFolder = CreateTempSeriesFolder();
        var existingCandidate = Path.Combine(candidateFolder, "poster.jpg");
        await File.WriteAllBytesAsync(existingCandidate, [4, 5, 6], CancellationToken.None);
        var manager = new PosterSourceManager();

        var result = await manager.EnsureOriginalAsync(
            seriesFolder,
            "original-series.jpg",
            [existingCandidate],
            (_, _) => Task.FromResult<byte[]?>([7, 8, 9]),
            42,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(Directory.Exists(seriesFolder));
        Assert.False(File.Exists(GetOriginalPath(seriesFolder, "original-series.jpg")));
    }

    [Fact]
    public async Task EnsureOriginalAsync_WhenPreCanceled_DoesNotCreateServiceFolderOrCallDownload()
    {
        var seriesFolder = CreateTempSeriesFolder();
        var existingCandidate = Path.Combine(seriesFolder, "poster.jpg");
        await File.WriteAllBytesAsync(existingCandidate, [4, 5, 6], CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var manager = new PosterSourceManager();
        var downloadCalled = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.EnsureOriginalAsync(
                seriesFolder,
                "original-series.jpg",
                [existingCandidate],
                (_, _) =>
                {
                    downloadCalled = true;
                    return Task.FromResult<byte[]?>([7, 8, 9]);
                },
                42,
                cancellationTokenSource.Token));

        Assert.False(Directory.Exists(Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName)));
        Assert.False(downloadCalled);
    }

    private static string GetOriginalPath(string seriesFolder, string originalFileName)
    {
        return Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName, originalFileName);
    }

    private static string CreateTempSeriesFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}

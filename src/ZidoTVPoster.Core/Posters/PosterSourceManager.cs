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
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(seriesFolder))
        {
            return PosterSourceResult.Failed($"Series folder does not exist: {seriesFolder}");
        }

        var serviceFolder = Path.Combine(seriesFolder, PosterStateStore.ServiceFolderName);
        var originalPath = Path.Combine(serviceFolder, originalFileName);

        if (File.Exists(originalPath))
        {
            return PosterSourceResult.Ok(originalPath, "Original poster already exists.");
        }

        foreach (var candidatePath in candidateLocalPosterPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(candidatePath))
            {
                continue;
            }

            Directory.CreateDirectory(serviceFolder);
            File.Copy(candidatePath, originalPath, overwrite: false);
            return PosterSourceResult.Ok(originalPath, $"Copied original poster from {candidatePath}.");
        }

        var bytes = await downloadOriginalAsync(zidooId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (bytes is not { Length: > 0 })
        {
            return PosterSourceResult.Failed($"No original poster source exists for Zidoo item {zidooId}.");
        }

        Directory.CreateDirectory(serviceFolder);
        await File.WriteAllBytesAsync(originalPath, bytes, cancellationToken);
        return PosterSourceResult.Ok(originalPath, $"Downloaded original poster for Zidoo item {zidooId}.");
    }
}

public sealed record PosterSourceResult(bool Success, string? OriginalPosterPath, string Message)
{
    public static PosterSourceResult Ok(string originalPosterPath, string message)
    {
        return new PosterSourceResult(true, originalPosterPath, message);
    }

    public static PosterSourceResult Failed(string message)
    {
        return new PosterSourceResult(false, null, message);
    }
}

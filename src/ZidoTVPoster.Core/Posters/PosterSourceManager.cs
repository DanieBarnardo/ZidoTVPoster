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
            CopyToOriginal(candidatePath, serviceFolder, originalPath, originalFileName, cancellationToken);
            return PosterSourceResult.Ok(originalPath, $"Copied original poster from {candidatePath}.");
        }

        var bytes = await downloadOriginalAsync(zidooId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (bytes is not { Length: > 0 })
        {
            return PosterSourceResult.Failed($"No original poster source exists for Zidoo item {zidooId}.");
        }

        Directory.CreateDirectory(serviceFolder);
        await WriteBytesToOriginalAsync(bytes, serviceFolder, originalPath, originalFileName, cancellationToken);
        return PosterSourceResult.Ok(originalPath, $"Downloaded original poster for Zidoo item {zidooId}.");
    }

    private static void CopyToOriginal(
        string sourcePath,
        string serviceFolder,
        string originalPath,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var tempPath = CreateTempPath(serviceFolder, originalFileName);
        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, originalPath, overwrite: true);
        }
        finally
        {
            DeleteTempFile(tempPath);
        }
    }

    private static async Task WriteBytesToOriginalAsync(
        byte[] bytes,
        string serviceFolder,
        string originalPath,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var tempPath = CreateTempPath(serviceFolder, originalFileName);
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, originalPath, overwrite: true);
        }
        finally
        {
            DeleteTempFile(tempPath);
        }
    }

    private static string CreateTempPath(string serviceFolder, string originalFileName)
    {
        return Path.Combine(serviceFolder, $"{Path.GetFileName(originalFileName)}.{Guid.NewGuid():N}.tmp");
    }

    private static void DeleteTempFile(string tempPath)
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
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

using System.Globalization;
using System.Xml;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterApplier
{
    public Task<PosterApplyResult> ApplyAsync(
        PosterUpdateItem item,
        string generatedPosterPath,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (dryRun)
        {
            return Task.FromResult(PosterApplyResult.Ok($"Dry run: would apply {generatedPosterPath} to {item.Kind} {item.ZidooId}."));
        }

        if (!File.Exists(generatedPosterPath))
        {
            return Task.FromResult(PosterApplyResult.Failed($"Generated poster does not exist: {generatedPosterPath}"));
        }

        if (!Directory.Exists(item.SeriesFolder))
        {
            return Task.FromResult(PosterApplyResult.Failed($"Series folder does not exist: {item.SeriesFolder}"));
        }

        var appliedPaths = item.Kind == PosterTargetKind.Series
            ? ApplySeriesPoster(item, generatedPosterPath, cancellationToken)
            : ApplySeasonPoster(item, generatedPosterPath, cancellationToken);

        return Task.FromResult(PosterApplyResult.Ok($"Applied local artwork: {string.Join(", ", appliedPaths)}."));
    }

    private static IReadOnlyList<string> ApplySeriesPoster(
        PosterUpdateItem item,
        string generatedPosterPath,
        CancellationToken cancellationToken)
    {
        var posterPath = Path.Combine(item.SeriesFolder, "poster.jpg");
        CopyAtomically(generatedPosterPath, posterPath, cancellationToken);

        var nfoPath = Path.Combine(item.SeriesFolder, "tvshow.nfo");
        WriteTvShowNfoAtomically(nfoPath, item, cancellationToken);

        return [posterPath, nfoPath];
    }

    private static IReadOnlyList<string> ApplySeasonPoster(
        PosterUpdateItem item,
        string generatedPosterPath,
        CancellationToken cancellationToken)
    {
        var appliedPaths = new List<string>();
        var rootSeasonPosterPath = Path.Combine(
            item.SeriesFolder,
            string.Create(CultureInfo.InvariantCulture, $"season{item.SeasonNumber:00}-poster.jpg"));
        CopyAtomically(generatedPosterPath, rootSeasonPosterPath, cancellationToken);
        appliedPaths.Add(rootSeasonPosterPath);

        foreach (var seasonFolder in FindSeasonFolders(item.SeriesFolder, item.SeasonNumber))
        {
            var seasonPosterPath = Path.Combine(seasonFolder, "poster.jpg");
            CopyAtomically(generatedPosterPath, seasonPosterPath, cancellationToken);
            appliedPaths.Add(seasonPosterPath);
        }

        return appliedPaths;
    }

    private static IReadOnlyList<string> FindSeasonFolders(string seriesFolder, int seasonNumber)
    {
        if (!Directory.Exists(seriesFolder) || seasonNumber <= 0)
        {
            return [];
        }

        var exactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            string.Create(CultureInfo.InvariantCulture, $"Season {seasonNumber}"),
            string.Create(CultureInfo.InvariantCulture, $"Season {seasonNumber:00}"),
            string.Create(CultureInfo.InvariantCulture, $"S{seasonNumber:00}")
        };

        return Directory.EnumerateDirectories(seriesFolder)
            .Where(path => exactNames.Contains(Path.GetFileName(path)))
            .ToArray();
    }

    private static void CopyAtomically(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException($"Destination path has no directory: {destinationPath}");
        }

        Directory.CreateDirectory(destinationDirectory);
        var tempPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void WriteTvShowNfoAtomically(
        string nfoPath,
        PosterUpdateItem item,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(nfoPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"NFO path has no directory: {nfoPath}");
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(nfoPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var writer = XmlWriter.Create(tempPath, new XmlWriterSettings
            {
                Async = false,
                Indent = true,
                Encoding = System.Text.Encoding.UTF8
            }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("tvshow");
                writer.WriteElementString("title", Path.GetFileName(item.SeriesFolder));
                writer.WriteStartElement("uniqueid");
                writer.WriteAttributeString("type", "zidoo");
                writer.WriteAttributeString("default", "true");
                writer.WriteString(string.Create(CultureInfo.InvariantCulture, $"zidoo-{item.ZidooId}"));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, nfoPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

public sealed record PosterApplyResult(bool Success, string Message)
{
    public static PosterApplyResult Ok(string message)
    {
        return new PosterApplyResult(true, message);
    }

    public static PosterApplyResult Failed(string message)
    {
        return new PosterApplyResult(false, message);
    }
}

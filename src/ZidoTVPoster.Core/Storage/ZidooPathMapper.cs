namespace ZidoTVPoster.Core.Storage;

public sealed record SeriesFolderMapResult(bool Success, string? SeriesFolder);

public sealed class ZidooPathMapper(string storageRoot, IReadOnlyCollection<string> mediaRootNames)
{
    public SeriesFolderMapResult MapSeriesFolder(string? uri)
    {
        return MapSeriesFolder(uri, storageRoot, mediaRootNames);
    }

    public static SeriesFolderMapResult MapSeriesFolder(
        string? uri,
        string storageRoot,
        IReadOnlyCollection<string> mediaRootNames)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return new SeriesFolderMapResult(false, null);
        }

        var segments = uri
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var isMediaRoot = mediaRootNames.Any(
                root => string.Equals(root, segment, StringComparison.OrdinalIgnoreCase));

            if (!isMediaRoot)
            {
                continue;
            }

            var seriesIndex = index + 1;
            if (seriesIndex >= segments.Length)
            {
                return new SeriesFolderMapResult(false, null);
            }

            return new SeriesFolderMapResult(
                true,
                Path.Combine(storageRoot, segments[seriesIndex]));
        }

        return new SeriesFolderMapResult(false, null);
    }
}

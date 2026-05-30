using System.Text.Json;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterStateStore
{
    public const string ServiceFolderName = ".zido-tv-poster";

    private const string StateFileName = "poster-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<PosterState?> LoadAsync(string seriesFolder, CancellationToken cancellationToken)
    {
        var statePath = GetStatePath(seriesFolder);
        if (!File.Exists(statePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<PosterState>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(string seriesFolder, PosterState state, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(seriesFolder))
        {
            throw new DirectoryNotFoundException($"Series folder does not exist: {seriesFolder}");
        }

        var serviceFolder = Path.Combine(seriesFolder, ServiceFolderName);
        Directory.CreateDirectory(serviceFolder);

        var statePath = GetStatePath(seriesFolder);
        await using var stream = File.Create(statePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    public static string GetStatePath(string seriesFolder)
    {
        return Path.Combine(seriesFolder, ServiceFolderName, StateFileName);
    }
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

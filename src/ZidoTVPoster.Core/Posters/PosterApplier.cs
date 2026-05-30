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

        return Task.FromResult(PosterApplyResult.Failed("Poster apply is not enabled until the Zidoo poster update route is verified."));
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

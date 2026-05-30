using System.Globalization;

namespace ZidoTVPoster.Core.Posters;

public static class PosterFileNames
{
    public const string OriginalSeriesPoster = "original-series-poster.jpg";
    public const string GeneratedSeriesPoster = "generated-series-poster.jpg";

    public static string OriginalSeasonPoster(int seasonNumber, int zidooId)
    {
        return SeasonPoster("original", seasonNumber, zidooId);
    }

    public static string GeneratedSeasonPoster(int seasonNumber, int zidooId)
    {
        return SeasonPoster("generated", seasonNumber, zidooId);
    }

    private static string SeasonPoster(string prefix, int seasonNumber, int zidooId)
    {
        if (seasonNumber > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix}-season-{seasonNumber:00}-poster.jpg");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}-season-zidoo-{zidooId}-poster.jpg");
    }
}

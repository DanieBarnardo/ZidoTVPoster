using System.Globalization;

namespace ZidoTVPoster.Core.Configuration;

public sealed class PosterUpdateOptions
{
    public int PollIntervalSeconds { get; set; } = 60;
    public bool DryRun { get; set; } = true;
    public bool HideBadgeWhenZero { get; set; } = true;
    public string BadgePlacement { get; set; } = "TopRight";
    public string BadgeTextFormat { get; set; } = "{0} unwatched";

    public static bool IsValid(PosterUpdateOptions options)
    {
        return options.PollIntervalSeconds > 0
            && !string.IsNullOrWhiteSpace(options.BadgeTextFormat)
            && IsBadgeTextFormatValid(options.BadgeTextFormat);
    }

    private static bool IsBadgeTextFormatValid(string badgeTextFormat)
    {
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, badgeTextFormat, 0);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

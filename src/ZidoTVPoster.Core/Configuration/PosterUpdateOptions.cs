namespace ZidoTVPoster.Core.Configuration;

public sealed class PosterUpdateOptions
{
    public int PollIntervalSeconds { get; set; } = 60;
    public bool DryRun { get; set; } = true;
    public bool HideBadgeWhenZero { get; set; } = true;
    public string BadgePlacement { get; set; } = "TopRight";
    public string BadgeTextFormat { get; set; } = "{0} unwatched";
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZidoTVPoster.Core.Zidoo;

public static class ZidooJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

public sealed record ZidooCollectionListResponse(
    int Status,
    IReadOnlyList<ZidooItem> Data);

public sealed record ZidooItem(
    int Id,
    int ParentId,
    int Type,
    string Name,
    bool Watched,
    ZidooAggregation? Aggregation = null,
    IReadOnlyList<ZidooItem>? Aggregations = null);

public sealed record ZidooDetailResponse(
    int Id,
    int ParentId,
    int Type,
    string Name,
    bool Watched,
    ZidooAggregation? Aggregation,
    IReadOnlyList<ZidooItem> Episodes,
    IReadOnlyList<ZidooItem>? Aggregations);

public sealed record ZidooAggregation(
    int Id = 0,
    int SeasonNumber = 0,
    int EpisodeNumber = 0,
    int EpisodeCount = 0,
    string TvName = "",
    string Uri = "",
    long LastWatchTime = 0,
    long PlayPoint = 0);

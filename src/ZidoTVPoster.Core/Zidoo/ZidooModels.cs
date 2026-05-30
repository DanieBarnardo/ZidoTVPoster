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

[JsonConverter(typeof(ZidooCollectionListResponseConverter))]
public sealed record ZidooCollectionListResponse(
    int Status,
    IReadOnlyList<ZidooItem> Data);

public sealed record ZidooItem
{
    [JsonConstructor]
    public ZidooItem(
        int Id,
        int ParentId,
        int Type,
        string Name,
        bool Watched,
        ZidooAggregation? Aggregation = null,
        IReadOnlyList<ZidooItem>? Aggregations = null,
        IReadOnlyList<ZidooItem>? Data = null)
    {
        this.Id = Id;
        this.ParentId = ParentId;
        this.Type = Type;
        this.Name = Name;
        this.Watched = Watched;
        this.Aggregation = Aggregation;
        this.Aggregations = Aggregations ?? Data;
        this.Data = Data;
    }

    public int Id { get; init; }

    public int ParentId { get; init; }

    public int Type { get; init; }

    public string Name { get; init; }

    public bool Watched { get; init; }

    public ZidooAggregation? Aggregation { get; init; }

    public IReadOnlyList<ZidooItem>? Aggregations { get; init; }

    public IReadOnlyList<ZidooItem>? Data { get; init; }
}

public sealed record ZidooDetailResponse
{
    [JsonConstructor]
    public ZidooDetailResponse(
        int Id,
        int ParentId,
        int Type,
        string Name,
        bool Watched,
        ZidooAggregation? Aggregation,
        IReadOnlyList<ZidooItem>? Episodes,
        IReadOnlyList<ZidooItem>? Aggregations,
        IReadOnlyList<ZidooItem>? Data = null)
    {
        this.Id = Id;
        this.ParentId = ParentId;
        this.Type = Type;
        this.Name = Name;
        this.Watched = Watched;
        this.Aggregation = Aggregation;
        this.Episodes = Episodes ?? Aggregations ?? Data ?? [];
        this.Aggregations = Aggregations;
        this.Data = Data;
    }

    public int Id { get; init; }

    public int ParentId { get; init; }

    public int Type { get; init; }

    public string Name { get; init; }

    public bool Watched { get; init; }

    public ZidooAggregation? Aggregation { get; init; }

    public IReadOnlyList<ZidooItem> Episodes { get; init; }

    public IReadOnlyList<ZidooItem>? Aggregations { get; init; }

    public IReadOnlyList<ZidooItem>? Data { get; init; }
}

public sealed record ZidooAggregation(
    int Id = 0,
    int SeasonNumber = 0,
    int EpisodeNumber = 0,
    int EpisodeCount = 0,
    string TvName = "",
    string Uri = "",
    long LastWatchTime = 0,
    long PlayPoint = 0);

internal sealed class ZidooCollectionListResponseConverter : JsonConverter<ZidooCollectionListResponse>
{
    public override ZidooCollectionListResponse Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = JsonSerializer.Deserialize<IReadOnlyList<ZidooItem>>(ref reader, options) ?? [];
            return new ZidooCollectionListResponse(200, items);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetInt32() : 200;
        var data = root.TryGetProperty("data", out var dataElement)
            ? JsonSerializer.Deserialize<IReadOnlyList<ZidooItem>>(dataElement.GetRawText(), options) ?? []
            : [];

        return new ZidooCollectionListResponse(status, data);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ZidooCollectionListResponse value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("status", value.Status);
        writer.WritePropertyName("data");
        JsonSerializer.Serialize(writer, value.Data, options);
        writer.WriteEndObject();
    }
}

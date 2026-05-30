using System.Net;
using System.Text.Json;
using ZidoTVPoster.Core.Zidoo;

namespace ZidoTVPoster.Tests;

public sealed class ZidooApiClientTests
{
    [Fact]
    public void CollectionListResponse_ParsesTvSeries()
    {
        const string json = """
        {
          "status": 200,
          "data": [
            { "id": 130, "parentId": -1, "type": 3, "name": "Band of Brothers", "watched": false },
            { "id": 291, "parentId": -1, "type": 2, "name": "A Quiet Place Collection", "watched": false }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<ZidooCollectionListResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Contains(response.Data, item => item.Id == 130 && item.Type == 3 && item.Name == "Band of Brothers");
    }

    [Fact]
    public void CollectionListResponse_ParsesBareArray()
    {
        const string json = """
        [
          { "id": 130, "parentId": -1, "type": 3, "name": "Band of Brothers", "watched": false },
          { "id": 291, "parentId": -1, "type": 2, "name": "A Quiet Place Collection", "watched": false }
        ]
        """;

        var response = JsonSerializer.Deserialize<ZidooCollectionListResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Contains(response.Data, item => item.Id == 130 && item.Type == 3 && item.Name == "Band of Brothers");
    }

    [Fact]
    public void DetailResponse_ParsesEpisodesAndMediaUris()
    {
        const string json = """
        {
          "id": 131,
          "parentId": 130,
          "type": 4,
          "name": "Season 1",
          "watched": false,
          "aggregation": {
            "id": "131",
            "seasonNumber": "1",
            "episodeCount": "1",
            "tvName": "Band of Brothers"
          },
          "episodes": [
            {
              "id": 401,
              "parentId": 131,
              "type": 1,
              "name": "Currahee",
              "watched": false,
              "aggregations": [
                {
                  "id": 402,
                  "parentId": 401,
                  "type": 0,
                  "name": "Band of Brothers - S01E01.mkv",
                  "watched": false,
                  "aggregation": {
                    "uri": "/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv",
                    "lastWatchTime": 0,
                    "playPoint": 0
                  }
                }
              ]
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<ZidooDetailResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        var episode = Assert.Single(response.Episodes);
        Assert.False(episode.Watched);
        var media = Assert.Single(episode.Aggregations!);
        Assert.Equal("/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv", media.Aggregation!.Uri);
    }

    [Fact]
    public void DetailResponse_UsesAggregationsAsEpisodesWhenEpisodesMissing()
    {
        const string json = """
        {
          "id": 131,
          "parentId": 130,
          "type": 4,
          "name": "Season 1",
          "watched": false,
          "aggregation": {
            "id": "131",
            "seasonNumber": "1",
            "episodeCount": "1",
            "tvName": "Band of Brothers"
          },
          "aggregations": [
            {
              "id": 401,
              "parentId": 131,
              "type": 5,
              "name": "Currahee",
              "watched": false,
              "aggregations": [
                {
                  "id": 402,
                  "parentId": 401,
                  "type": 0,
                  "name": "Band of Brothers - S01E01.mkv",
                  "watched": false,
                  "aggregation": {
                    "uri": "/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv"
                  }
                }
              ]
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<ZidooDetailResponse>(json, ZidooJson.Options);

        Assert.NotNull(response);
        var episode = Assert.Single(response.Episodes);
        Assert.Equal(401, episode.Id);
        Assert.Equal("/Series/Band of Brothers/Season 1/Band of Brothers - S01E01.mkv", Assert.Single(episode.Aggregations!).Aggregation!.Uri);
    }

    [Fact]
    public void ZidooItem_MapsDataToAggregations()
    {
        const string json = """
        {
          "id": 130,
          "parentId": -1,
          "type": 3,
          "name": "Band of Brothers",
          "watched": false,
          "data": [
            { "id": 131, "parentId": 130, "type": 4, "name": "Season 1", "watched": false }
          ]
        }
        """;

        var item = JsonSerializer.Deserialize<ZidooItem>(json, ZidooJson.Options);

        Assert.NotNull(item);
        var season = Assert.Single(item.Aggregations!);
        Assert.Equal(131, season.Id);
        Assert.Equal(4, season.Type);
    }

    [Fact]
    public async Task GetCollectionListAsync_UsesZidooEndpointAndDeserializesResponse()
    {
        const string json = """
        {
          "status": 200,
          "data": [
            { "id": 130, "parentId": -1, "type": 3, "name": "Band of Brothers", "watched": false }
          ]
        }
        """;
        using var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var client = new ZidooApiClient(httpClient);

        var response = await client.GetCollectionListAsync(CancellationToken.None);

        Assert.Equal("/ZidooPoster/getCollectionList", handler.RequestUri?.PathAndQuery);
        Assert.Equal(200, response.Status);
        Assert.Equal("Band of Brothers", Assert.Single(response.Data).Name);
    }

    [Fact]
    public async Task GetCollectionAsync_UsesCollectionEndpoint()
    {
        const string json = """
        {
          "id": 130,
          "parentId": -1,
          "type": 3,
          "name": "Band of Brothers",
          "watched": false
        }
        """;
        using var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var client = new ZidooApiClient(httpClient);

        var response = await client.GetCollectionAsync(130, CancellationToken.None);

        Assert.Equal("/ZidooPoster/getCollection?id=130", handler.RequestUri?.PathAndQuery);
        Assert.Equal("Band of Brothers", response.Name);
    }

    [Fact]
    public async Task GetDetailAsync_UsesDetailEndpoint()
    {
        const string json = """
        {
          "id": 131,
          "parentId": 130,
          "type": 4,
          "name": "Season 1",
          "watched": false,
          "episodes": []
        }
        """;
        using var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://zidoo.local")
        };
        var client = new ZidooApiClient(httpClient);

        var response = await client.GetDetailAsync(131, CancellationToken.None);

        Assert.Equal("/ZidooPoster/getDetail?id=131", handler.RequestUri?.PathAndQuery);
        Assert.Equal("Season 1", response.Name);
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
            return Task.FromResult(response);
        }
    }
}

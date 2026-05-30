using System.Net.Http.Json;

namespace ZidoTVPoster.Core.Zidoo;

public sealed class ZidooApiClient(HttpClient httpClient)
{
    public async Task<ZidooCollectionListResponse> GetCollectionListAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/ZidooPoster/getCollectionList", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ZidooCollectionListResponse>(ZidooJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Zidoo collection list response was empty.");
    }

    public async Task<ZidooItem> GetCollectionAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/ZidooPoster/getCollection?id={id}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ZidooItem>(ZidooJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Zidoo collection response for {id} was empty.");
    }

    public async Task<ZidooDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/ZidooPoster/getDetail?id={id}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ZidooDetailResponse>(ZidooJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Zidoo detail response for {id} was empty.");
    }

    public async Task<byte[]?> GetPosterBytesAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/ZidooPoster/getFile/getPoster?id={id}&w=1000&h=1500", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}

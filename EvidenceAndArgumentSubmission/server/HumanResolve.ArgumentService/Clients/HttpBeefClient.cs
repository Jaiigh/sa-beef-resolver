using System.Net;
using System.Text.Json.Serialization;

namespace HumanResolve.ArgumentService.Clients;

public sealed class HttpBeefClient(HttpClient http) : IBeefClient
{
    private sealed record BeefDto(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("person_a")] string PersonA,
        [property: JsonPropertyName("person_b")] string PersonB,
        [property: JsonPropertyName("status")] string Status);

    public async Task<BeefInfo?> GetBeefAsync(long beefId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync($"api/beefs/{beefId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<BeefDto>(cancellationToken)
                ?? throw new BeefServiceUnavailableException("Beef API returned an empty response");
            return new BeefInfo(dto.Id, dto.PersonA, dto.PersonB, dto.Status);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new BeefServiceUnavailableException("Beef API is unavailable", ex);
        }
    }
}

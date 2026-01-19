using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UI.Services;

public sealed class SpotifyAuthServiceClient : ISpotifyAuthService
{
    private readonly HttpClient _httpClient;

    public SpotifyAuthServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetAuthorizationUrlAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<SpotifyAuthResponse>("spotify/authorize", cancellationToken)
            .ConfigureAwait(false);

        return response?.Url ?? string.Empty;
    }

    private sealed record SpotifyAuthResponse(string Url, string State);
}

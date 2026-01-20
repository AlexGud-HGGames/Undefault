using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;

namespace UI.Services;

public sealed class HttpProfileService : IProfileService
{
    private readonly HttpClient _httpClient;

    public HttpProfileService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MusicProfilesConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _httpClient.GetFromJsonAsync<MusicProfilesConfig>("profiles", cancellationToken)
            .ConfigureAwait(false);

        return config ?? throw new InvalidOperationException("Profiles response was empty.");
    }

    public async Task SaveAsync(MusicProfilesConfig config, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("profiles", config, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}

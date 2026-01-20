using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;

namespace UI.Services;

public sealed class HttpConfigurationService : IConfigurationService
{
    private readonly HttpClient _httpClient;

    public HttpConfigurationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SystemConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _httpClient.GetFromJsonAsync<SystemConfig>("config", cancellationToken)
            .ConfigureAwait(false);

        return config ?? throw new InvalidOperationException("Configuration response was empty.");
    }

    public async Task SaveAsync(SystemConfig config, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("config", config, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}

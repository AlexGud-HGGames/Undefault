using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Json;
using Cs2Simulator.Scenarios.Models;
using Microsoft.Extensions.Logging;

namespace Cs2Simulator.Runtime;

public sealed class HttpGsiTransport : IGsiTransport
{
    private const int MaxBodyExcerpt = 512;

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpGsiTransport> _logger;

    public HttpGsiTransport(HttpClient httpClient, ILogger<HttpGsiTransport> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendAsync(Cs2Payload payload, CancellationToken ct)
    {
        var json = Cs2PayloadJson.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("gsi", content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadBodyExcerptAsync(response, ct).ConfigureAwait(false);
            throw new GsiTransportException(
                $"POST gsi failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        _logger.LogTrace("POST gsi ok ({StatusCode})", (int)response.StatusCode);
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        using var response = await _httpClient.PostAsync("gsi/reset", content: null, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadBodyExcerptAsync(response, ct).ConfigureAwait(false);
            throw new GsiTransportException(
                $"POST gsi/reset failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        _logger.LogDebug("POST gsi/reset ok ({StatusCode})", (int)response.StatusCode);
    }

    private static async Task<string> ReadBodyExcerptAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return body.Length <= MaxBodyExcerpt ? body : body.Substring(0, MaxBodyExcerpt) + "...";
        }
        catch (Exception ex)
        {
            return $"<could not read body: {ex.Message}>";
        }
    }
}

public sealed class GsiTransportException : Exception
{
    public GsiTransportException(string message) : base(message) { }
}

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Core.Configuration;
using Core.Spotify;
using FluentAssertions;
using GsiHost.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GsiHost.Tests;

/// <summary>
/// UND-47 AC #11–13. Captures every log line emitted by <see cref="SpotifyOAuthService"/>,
/// <see cref="SpotifyClient"/>, <see cref="ConsoleLaunchBootstrap"/>, and
/// <see cref="Cs2SetupService"/> across a representative auth + playback round-trip and
/// fails fast if any line contains a configured client_id, a probe client_secret value,
/// a token-shaped string, or any of the dummy access/refresh token literals.
///
/// Placement rationale: this test crosses both Core and GsiHost service boundaries, so it
/// lives in <c>GsiHost.Tests</c> — the only project that already references both. The
/// <c>Cs2Simulator.Tests</c> project does not depend on Core or GsiHost services.
/// </summary>
public sealed class CredentialAndTokenLoggingRedactionTests
{
    // Dummy values are chosen to be obviously synthetic but long enough that an accidental
    // log substring will collide. They are passed through every code path under test.
    private const string DummyClientId = "dummy-client-id-12345";
    private const string DummyClientSecretProbe = "dummy-client-secret-67890";
    private const string DummyAccessToken = "dummy-access-token-abcde";
    private const string DummyRefreshToken = "dummy-refresh-token-fghij";

    [Fact]
    public async Task NoLoggerLeaksClientCredentialsOrTokens_AcrossAuthAndPlaybackRoundTrip()
    {
        var sink = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        // 1) ConsoleLaunchBootstrap: probe with the legacy CLIENT_SECRET env var set to
        //    confirm its presence-only debug log never echoes the value.
        var previousClientId = Environment.GetEnvironmentVariable("CLIENT_ID");
        var previousClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
        Environment.SetEnvironmentVariable("CLIENT_ID", null);
        Environment.SetEnvironmentVariable("CLIENT_SECRET", DummyClientSecretProbe);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Spotify:ClientId"] = DummyClientId,
                    ["Gsi:Url"] = "http://127.0.0.1:5292"
                }).Build();

            ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: false,
                new NullPrompter(),
                new InMemorySpotifySecretStore(),
                logger: loggerFactory.CreateLogger("ConsoleLaunchBootstrap"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLIENT_ID", previousClientId);
            Environment.SetEnvironmentVariable("CLIENT_SECRET", previousClientSecret);
        }

        // 2) SpotifyOAuthService: full PKCE round-trip (authorize → exchange → refresh)
        //    with a fake handler returning the dummy tokens.
        var tokenHandler = new DummyTokenHandler(DummyAccessToken, DummyRefreshToken);
        var oauth = new SpotifyOAuthService(
            new SingleHandlerHttpClientFactory(tokenHandler),
            Options.Create(new SpotifyClientOptions
            {
                ClientId = DummyClientId,
                RedirectUri = "http://127.0.0.1:5292/callback"
            }),
            loggerFactory.CreateLogger<SpotifyOAuthService>());

        _ = oauth.GetAuthorizationUrl(state: "redaction-state");
        var exchanged = await oauth.ExchangeCodeForTokenAsync("auth-code", state: "redaction-state");
        await oauth.RefreshTokenAsync(exchanged.RefreshToken);

        // 3) SpotifyClient: authenticated playback round-trip. We seed expired tokens so
        //    the next call forces SpotifyClient's "Refreshing access token" log line to
        //    fire — that is the one place it touches the logger today.
        var tokenStorage = new InMemoryTokenStorage();
        await tokenStorage.SaveTokensAsync(
            DummyAccessToken,
            DummyRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var apiHandler = new ApiNoContentHandler();
        var spotifyClient = new SpotifyClient(
            new SingleHandlerHttpClientFactory(apiHandler),
            tokenStorage,
            oauth,
            Options.Create(new SpotifyClientOptions { ClientId = DummyClientId }),
            loggerFactory.CreateLogger<SpotifyClient>());

        await spotifyClient.PauseAsync();
        await spotifyClient.ResumeAsync();
        await spotifyClient.SetVolumeAsync(50);

        // 4) Cs2SetupService: deliberately route into its catch/log path by failing the
        //    config read. GetStatusAsync wraps BuildGsiUriAsync in try/catch and emits a
        //    LogWarning — this is the only place the service ever logs today.
        var cs2Setup = new Cs2SetupService(
            new ThrowingConfigurationService(),
            loggerFactory.CreateLogger<Cs2SetupService>());
        _ = await cs2Setup.GetStatusAsync();

        // ---- Assertions ----
        var captured = sink.Snapshot();
        captured.Should().NotBeEmpty("at least one of the four loggers must have emitted something during the round-trip");

        var bannedLiterals = new[]
        {
            DummyClientSecretProbe,
            DummyAccessToken,
            DummyRefreshToken
        };

        // `Bearer <opaque>` is the only token-shaped wire pattern the host could ever
        // emit; refuse it outright.
        var bearerPattern = new Regex(@"Bearer\s+\S+", RegexOptions.IgnoreCase);

        foreach (var entry in captured)
        {
            var rendered = entry.RenderedMessage;
            foreach (var banned in bannedLiterals)
            {
                rendered.Should().NotContain(
                    banned,
                    "logger '{0}' (level {1}) emitted a forbidden literal: {2}",
                    entry.Category,
                    entry.Level,
                    rendered);
            }

            bearerPattern.IsMatch(rendered).Should().BeFalse(
                $"logger '{entry.Category}' (level {entry.Level}) emitted a Bearer-token-shaped string: {rendered}");
        }

        // client_id is "public" per Spotify's docs (it appears in every authorize URL)
        // but UND-47's compliance cross-check disallows it from startup logs as well, so
        // we hold the host to that bar across all four loggers.
        foreach (var entry in captured)
        {
            entry.RenderedMessage.Should().NotContain(
                DummyClientId,
                "logger '{0}' (level {1}) emitted the configured client_id: {2}",
                entry.Category,
                entry.Level,
                entry.RenderedMessage);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<CapturedLogEntry> _entries = new();
        private readonly object _lock = new();

        public IReadOnlyList<CapturedLogEntry> Snapshot()
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly CapturingLoggerProvider _provider;

            public CapturingLogger(string category, CapturingLoggerProvider provider)
            {
                _category = category;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var rendered = formatter(state, exception);
                if (exception is not null)
                {
                    rendered = $"{rendered}\n{exception}";
                }

                lock (_provider._lock)
                {
                    _provider._entries.Add(new CapturedLogEntry(_category, logLevel, rendered));
                }
            }
        }
    }

    private sealed record CapturedLogEntry(string Category, LogLevel Level, string RenderedMessage);

    private sealed class NullPrompter : IConsoleCredentialPrompter
    {
        public string? ReadValue(string prompt) => null;
        public string? ReadSecret(string prompt) => null;
    }

    private sealed class InMemorySpotifySecretStore : ISpotifySecretStore
    {
        private SpotifyLocalSecrets? _stored;

        public string FilePath => "C:\\redaction-test\\spotify-secrets.bin";
        public bool Exists() => _stored is not null;
        public SpotifyLocalSecrets? TryLoad() => _stored;
        public void Save(SpotifyLocalSecrets secrets) => _stored = secrets;
        public void Delete() => _stored = null;
    }

    private sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleHandlerHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class DummyTokenHandler : HttpMessageHandler
    {
        private readonly string _accessToken;
        private readonly string _refreshToken;

        public DummyTokenHandler(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = $$"""
            {
              "access_token": "{{_accessToken}}",
              "token_type": "Bearer",
              "scope": "user-modify-playback-state user-read-playback-state",
              "expires_in": 3600,
              "refresh_token": "{{_refreshToken}}"
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ApiNoContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class ThrowingConfigurationService : IConfigurationService
    {
        public Task<SystemConfig> GetAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("synthetic failure for redaction test");

        public Task SaveAsync(SystemConfig config, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

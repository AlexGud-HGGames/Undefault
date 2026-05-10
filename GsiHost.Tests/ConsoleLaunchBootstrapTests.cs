using FluentAssertions;
using GsiHost.Services;
using Microsoft.Extensions.Configuration;
using Core.Spotify;
using Microsoft.Extensions.Logging.Abstractions;

namespace GsiHost.Tests;

public sealed class ConsoleLaunchBootstrapTests
{
    [Fact]
    public void Prepare_UsesExistingClientIdWithoutPrompting_AndForcesRealSpotify()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            // UND-47: PKCE flow has no client_secret. A configured client_id alone is
            // sufficient to skip prompting.
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["UseMockSpotify"] = "true",
                ["Spotify:ClientId"] = "configured-client-id",
                ["Spotify:RedirectUri"] = "http://127.0.0.1:5292/callback",
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var prompter = new FakeConsoleCredentialPrompter();
            var store = new FakeSpotifySecretStore();

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: false,
                prompter,
                store);

            settings.HasSpotifyCredentials.Should().BeTrue();
            settings.PromptedForCredentials.Should().BeFalse();
            settings.ConfigurationOverrides["UseMockSpotify"].Should().Be("false");
            settings.LoadedFromEncryptedStore.Should().BeFalse();
            settings.ConfigurationOverrides.ContainsKey("Spotify:ClientSecret").Should().BeFalse(
                "PKCE flow no longer carries a client_secret");
            prompter.ValuePrompts.Should().Be(0);
        });
    }

    [Fact]
    public void Prepare_NormalizesLoopbackUrls()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Spotify:RedirectUri"] = "http://localhost:5292/callback",
                ["Gsi:Url"] = "http://localhost:5292"
            });
            var store = new FakeSpotifySecretStore();

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                store);

            settings.GsiBaseUrl.Should().Be("http://127.0.0.1:5292");
            settings.RedirectUri.Should().Be("http://127.0.0.1:5292/callback");
        });
    }

    [Fact]
    public void Prepare_PromptsForMissingClientId_WhenInteractive_AndDoesNotAskForClientSecret()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var prompter = new FakeConsoleCredentialPrompter("prompted-client-id");
            var store = new FakeSpotifySecretStore();

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: true,
                prompter,
                store);

            settings.HasSpotifyCredentials.Should().BeTrue();
            settings.PromptedForCredentials.Should().BeTrue();
            settings.SavedToEncryptedStore.Should().BeTrue();
            settings.ConfigurationOverrides["Spotify:ClientId"].Should().Be("prompted-client-id");
            settings.ConfigurationOverrides.ContainsKey("Spotify:ClientSecret").Should().BeFalse(
                "PKCE flow has no client_secret");
            store.SavedSecrets.Should().BeEquivalentTo(new SpotifyLocalSecrets("prompted-client-id"));
            prompter.ValuePrompts.Should().Be(1);
        });
    }

    [Fact]
    public void Prepare_LoadsClientIdFromEncryptedStore_WithoutPrompting()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var store = new FakeSpotifySecretStore
            {
                StoredSecrets = new SpotifyLocalSecrets("stored-client-id")
            };

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: true,
                new FakeConsoleCredentialPrompter(),
                store);

            settings.HasSpotifyCredentials.Should().BeTrue();
            settings.LoadedFromEncryptedStore.Should().BeTrue();
            settings.PromptedForCredentials.Should().BeFalse();
            settings.ConfigurationOverrides["Spotify:ClientId"].Should().Be("stored-client-id");
            settings.ConfigurationOverrides.ContainsKey("Spotify:ClientSecret").Should().BeFalse();
        });
    }

    [Fact]
    public void Prepare_QuickLaunch_UsesMockSpotify_NoPrompt_AndSkipsOptionalStartup()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });

            var prompter = new FakeConsoleCredentialPrompter("prompted-client-id");
            var store = new FakeSpotifySecretStore();

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--quick" },
                isInteractiveConsole: true,
                prompter,
                store);

            settings.IsQuickLaunch.Should().BeTrue();
            settings.SkipCs2Setup.Should().BeTrue();
            settings.SkipSmartTrackWarmup.Should().BeTrue();

            settings.HasSpotifyCredentials.Should().BeFalse();
            settings.PromptedForCredentials.Should().BeFalse();
            settings.ConfigurationOverrides["UseMockSpotify"].Should().Be("true");

            prompter.ValuePrompts.Should().Be(0);
        });
    }

    [Fact]
    public void Prepare_UseRealSpotifyOverridesQuickLaunchAndRestoresNormalStartup()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--quick", "--use-real-spotify" },
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                new FakeSpotifySecretStore());

            settings.IsQuickLaunch.Should().BeFalse();
            settings.SkipCs2Setup.Should().BeFalse();
            settings.SkipSmartTrackWarmup.Should().BeFalse();
            settings.ConfigurationOverrides["UseMockSpotify"].Should().Be("false");
        });
    }

    [Fact]
    public async Task MockSpotifyClient_ReportsUnauthenticatedState()
    {
        var client = new MockSpotifyClient(NullLogger<MockSpotifyClient>.Instance);

        (await client.IsAuthenticatedAsync()).Should().BeFalse();
    }

    [Fact]
    public void Prepare_ResetFlag_OverwritesEncryptedStore()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var store = new FakeSpotifySecretStore
            {
                StoredSecrets = new SpotifyLocalSecrets("old-client-id")
            };
            var prompter = new FakeConsoleCredentialPrompter("new-client-id");

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--reset-spotify-secrets" },
                isInteractiveConsole: true,
                prompter,
                store);

            settings.ResetEncryptedStoreRequested.Should().BeTrue();
            settings.SavedToEncryptedStore.Should().BeTrue();
            settings.LoadedFromEncryptedStore.Should().BeFalse();
            store.SavedSecrets.Should().BeEquivalentTo(new SpotifyLocalSecrets("new-client-id"));
        });
    }

    [Fact]
    public void Prepare_ClearSpotifySecretsFlag_DeletesEncryptedStore_AndDoesNotThrow_WhenStoreIsEmpty()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            // UND-47: with PKCE there may be nothing to clear. The flag still wipes the
            // store on demand without erroring out.
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var emptyStore = new FakeSpotifySecretStore();

            var settingsEmpty = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--clear-spotify-secrets" },
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                emptyStore);

            settingsEmpty.ClearedEncryptedStore.Should().BeFalse(
                "an empty store has nothing to clear, but the flag must not throw");

            var populatedStore = new FakeSpotifySecretStore
            {
                StoredSecrets = new SpotifyLocalSecrets("cached-client-id")
            };

            var settingsPopulated = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--clear-spotify-secrets" },
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                populatedStore);

            settingsPopulated.ClearedEncryptedStore.Should().BeTrue();
            populatedStore.StoredSecrets.Should().BeNull();
        });
    }

    [Fact]
    public void Prepare_LegacyClientSecretEnvVarPresent_IsSurfaced_ButNotConsumed()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            Environment.SetEnvironmentVariable("CLIENT_SECRET", "legacy-secret-should-be-ignored");

            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Spotify:ClientId"] = "configured-client-id",
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var store = new FakeSpotifySecretStore();

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                Array.Empty<string>(),
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                store);

            settings.LegacyClientSecretEnvVarPresent.Should().BeTrue();
            settings.HasSpotifyCredentials.Should().BeTrue("client_id alone is enough under PKCE");
            settings.ConfigurationOverrides.ContainsKey("Spotify:ClientSecret").Should().BeFalse();
        });
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void RunWithoutSpotifyEnvVars(Action action)
    {
        var previousClientId = Environment.GetEnvironmentVariable("CLIENT_ID");
        var previousClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
        Environment.SetEnvironmentVariable("CLIENT_ID", null);
        Environment.SetEnvironmentVariable("CLIENT_SECRET", null);

        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLIENT_ID", previousClientId);
            Environment.SetEnvironmentVariable("CLIENT_SECRET", previousClientSecret);
        }
    }

    private sealed class FakeConsoleCredentialPrompter : IConsoleCredentialPrompter
    {
        private readonly string? _value;

        public FakeConsoleCredentialPrompter(string? value = null)
        {
            _value = value;
        }

        public int ValuePrompts { get; private set; }

        public string? ReadValue(string prompt)
        {
            ValuePrompts++;
            return _value;
        }
    }

    private sealed class FakeSpotifySecretStore : ISpotifySecretStore
    {
        public string FilePath => "C:\\fake\\spotify-secrets.bin";

        public SpotifyLocalSecrets? StoredSecrets { get; set; }

        public SpotifyLocalSecrets? SavedSecrets { get; private set; }

        public bool Exists()
        {
            return StoredSecrets is not null;
        }

        public SpotifyLocalSecrets? TryLoad()
        {
            return StoredSecrets;
        }

        public void Save(SpotifyLocalSecrets secrets)
        {
            SavedSecrets = secrets;
            StoredSecrets = secrets;
        }

        public void Delete()
        {
            StoredSecrets = null;
        }
    }
}

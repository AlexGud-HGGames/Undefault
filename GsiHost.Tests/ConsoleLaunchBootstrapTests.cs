using FluentAssertions;
using GsiHost.Services;
using Microsoft.Extensions.Configuration;
using Core.Spotify;
using Microsoft.Extensions.Logging.Abstractions;

namespace GsiHost.Tests;

public sealed class ConsoleLaunchBootstrapTests
{
    [Fact]
    public void Prepare_UsesExistingCredentialsWithoutPrompting_AndForcesRealSpotify()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["UseMockSpotify"] = "true",
                ["Spotify:ClientId"] = "configured-client-id",
                ["Spotify:ClientSecret"] = "configured-client-secret",
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
            prompter.ValuePrompts.Should().Be(0);
            prompter.SecretPrompts.Should().Be(0);
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
    public void Prepare_PromptsForMissingCredentials_WhenInteractive()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var prompter = new FakeConsoleCredentialPrompter("prompted-client-id", "prompted-client-secret");
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
            settings.ConfigurationOverrides["Spotify:ClientSecret"].Should().Be("prompted-client-secret");
            store.SavedSecrets.Should().BeEquivalentTo(new SpotifyLocalSecrets("prompted-client-id", "prompted-client-secret"));
            prompter.ValuePrompts.Should().Be(1);
            prompter.SecretPrompts.Should().Be(1);
        });
    }

    [Fact]
    public void Prepare_LoadsSecretsFromEncryptedStore_WithoutPrompting()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292"
            });
            var store = new FakeSpotifySecretStore
            {
                StoredSecrets = new SpotifyLocalSecrets("stored-client-id", "stored-client-secret")
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
            settings.ConfigurationOverrides["Spotify:ClientSecret"].Should().Be("stored-client-secret");
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

            var prompter = new FakeConsoleCredentialPrompter("prompted-client-id", "prompted-client-secret");
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
            prompter.SecretPrompts.Should().Be(0);
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
    public void Prepare_RuntimeModeFlagsOverrideConfiguredMode()
    {
        RunWithoutSpotifyEnvVars(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Gsi:Url"] = "http://127.0.0.1:5292",
                ["Runtime:Mode"] = "scenario_playback"
            });

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--intent-capture" },
                isInteractiveConsole: false,
                new FakeConsoleCredentialPrompter(),
                new FakeSpotifySecretStore());

            settings.ConfigurationOverrides["Runtime:Mode"].Should().Be("intent_capture");
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
                StoredSecrets = new SpotifyLocalSecrets("old-client-id", "old-client-secret")
            };
            var prompter = new FakeConsoleCredentialPrompter("new-client-id", "new-client-secret");

            var settings = ConsoleLaunchBootstrap.Prepare(
                configuration,
                new[] { "--reset-spotify-secrets" },
                isInteractiveConsole: true,
                prompter,
                store);

            settings.ResetEncryptedStoreRequested.Should().BeTrue();
            settings.SavedToEncryptedStore.Should().BeTrue();
            settings.LoadedFromEncryptedStore.Should().BeFalse();
            store.SavedSecrets.Should().BeEquivalentTo(new SpotifyLocalSecrets("new-client-id", "new-client-secret"));
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
        private readonly string? _secret;

        public FakeConsoleCredentialPrompter(string? value = null, string? secret = null)
        {
            _value = value;
            _secret = secret;
        }

        public int ValuePrompts { get; private set; }

        public int SecretPrompts { get; private set; }

        public string? ReadValue(string prompt)
        {
            ValuePrompts++;
            return _value;
        }

        public string? ReadSecret(string prompt)
        {
            SecretPrompts++;
            return _secret;
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

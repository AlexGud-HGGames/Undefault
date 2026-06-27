using Core.Configuration;
using FluentAssertions;
using GsiHost.Configuration;
using GsiHost.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace GsiHost.Tests;

/// <summary>
/// Verifies that the git-tracked default <c>Keybinds.Bindings</c> in
/// <c>GsiHost/appsettings.json</c> resolve through the default
/// <c>control-profiles.json</c> rules produced by <see cref="JsonControlProfileService"/>
/// to the expected pause / resume / duck commands, and that every default
/// binding parses to a registerable <see cref="HotkeyDefinition"/>. This is the
/// pragmatic config-resolution equivalent of asserting the intent_capture host
/// registers the expected binding count (tech steer: prefer config resolution
/// over a heavy Win32 mockable seam).
/// </summary>
public class DefaultKeybindResolutionTests
{
    [Fact]
    public async Task DefaultBindings_ResolveToExpectedControlProfileCommands()
    {
        var keybindOptions = LoadDefaultKeybindOptions();
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["custom:music_pause"] = MusicControlCommands.Pause,
            ["custom:music_resume"] = MusicControlCommands.Resume,
            ["custom:music_mute"] = MusicControlCommands.Duck
        };

        keybindOptions.Bindings
            .Select(b => b.EventKey)
            .Should().BeEquivalentTo(expected.Keys, "the default bindings must be exactly the three MVP manual keys");

        var tempRoot = Path.Combine(Path.GetTempPath(), "UndefaultIt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // No control-profiles.json present -> the service generates the default profile.
            var env = new FakeWebHostEnvironment { ContentRootPath = tempRoot };
            var service = new JsonControlProfileService(env, NullLogger<JsonControlProfileService>.Instance);
            var profiles = await service.GetAsync();

            var profile = profiles.Profiles.First();
            foreach (var binding in keybindOptions.Bindings)
            {
                var rule = profile.FindRule(binding.EventKey!);
                rule.Should().NotBeNull("default profile must define a rule for default binding {0}", binding.EventKey);
                rule!.Command.Should().Be(
                    expected[binding.EventKey!],
                    "default binding {0} must resolve to the expected command", binding.EventKey);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void DefaultBindings_AllParseToValidHotkeys_WithExpectedCount()
    {
        var keybindOptions = LoadDefaultKeybindOptions();

        keybindOptions.Enabled.Should().BeFalse(
            "git-tracked appsettings default keeps keybinds off until --mvp / per-feature flag enables them");
        keybindOptions.Bindings.Should().HaveCount(3, "the MVP ships three default manual hotkey bindings");

        keybindOptions.Bindings
            .Select(b => HotkeyDefinition.TryParse(b.Key))
            .Should()
            .AllSatisfy(h => h.Should().NotBeNull("every default binding must parse to a registerable hotkey"));
    }

    private static KeybindOptions LoadDefaultKeybindOptions()
    {
        var path = LocateGsiHostAppSettings();
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        return config.GetSection(KeybindOptions.SectionName).Get<KeybindOptions>()
            ?? new KeybindOptions();
    }

    private static string LocateGsiHostAppSettings()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "GsiHost", "appsettings.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate GsiHost/appsettings.json from the test output directory.",
            "GsiHost/appsettings.json");
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "GsiHost.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

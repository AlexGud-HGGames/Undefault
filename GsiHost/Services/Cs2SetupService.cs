using System.Text.RegularExpressions;
using Core.Configuration;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class Cs2SetupService : ICs2SetupService
{
    private static readonly string[] DefaultInstallPaths =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
        @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
        @"E:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
    };

    private const string CfgFileName = "gamestate_integration_undefaultit.cfg";

    private static readonly Regex NestedPathRegex =
        new("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LegacyPathRegex =
        new("^\\s*\"\\d+\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.Compiled);
    private const string Cs2PathOverrideEnvVar = "UNDEFAULTIT_CS2_PATH";

    private readonly IConfigurationService _configurationService;
    private readonly ILogger<Cs2SetupService> _logger;

    public Cs2SetupService(
        IConfigurationService configurationService,
        ILogger<Cs2SetupService> logger)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<Cs2SetupStatus> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var gsiUri = await BuildGsiUriAsync(ct);
            var cs2Path = FindCs2Path();
            if (cs2Path is null)
            {
                return new Cs2SetupStatus(false, null, false, false, null, gsiUri, false, "CS2 not found");
            }

            var cfgPath = BuildCfgPath(cs2Path);
            var isCfgInstalled = File.Exists(cfgPath);
            var expectedContent = BuildGsiConfigContent(gsiUri);
            var isCfgCurrent = isCfgInstalled && await HasExpectedContentAsync(cfgPath, expectedContent, ct);

            return new Cs2SetupStatus(
                true,
                cs2Path,
                isCfgInstalled,
                isCfgCurrent,
                cfgPath,
                gsiUri,
                isCfgInstalled && isCfgCurrent,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine CS2 setup status");
            return new Cs2SetupStatus(false, null, false, false, null, null, false, ex.Message);
        }
    }

    public async Task<Cs2SetupResult> InstallAsync(CancellationToken ct = default)
    {
        var gsiUri = await BuildGsiUriAsync(ct);
        var cs2Path = FindCs2Path();
        if (cs2Path is null)
        {
            return new Cs2SetupResult(false, null, gsiUri, false, "CS2 not found");
        }

        var cfgPath = BuildCfgPath(cs2Path);
        try
        {
            var cfgDir = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrWhiteSpace(cfgDir))
            {
                Directory.CreateDirectory(cfgDir);
            }

            await File.WriteAllTextAsync(cfgPath, BuildGsiConfigContent(gsiUri), ct);
            return new Cs2SetupResult(true, cfgPath, gsiUri, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to install CS2 GSI config");
            return new Cs2SetupResult(false, null, gsiUri, false, ex.Message);
        }
    }

    public async Task<Cs2SetupResult> EnsureInstalledAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.IsCs2Found)
        {
            return new Cs2SetupResult(false, null, status.GsiUri, false, status.Error ?? "CS2 not found");
        }

        if (status.IsReady)
        {
            return new Cs2SetupResult(true, status.CfgPath, status.GsiUri, false, null);
        }

        return await InstallAsync(ct);
    }

    private static string? FindCs2Path()
    {
        foreach (var candidate in GetCandidateCs2Roots())
        {
            var cfgDir = Path.Combine(candidate, "game", "csgo", "cfg");
            if (Directory.Exists(cfgDir))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateCs2Roots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var overridePath = Environment.GetEnvironmentVariable(Cs2PathOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && seen.Add(overridePath))
        {
            yield return overridePath;
        }

        foreach (var path in DefaultInstallPaths)
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (var library in GetSteamLibraries())
        {
            var cs2Root = Path.Combine(library, "steamapps", "common", "Counter-Strike Global Offensive");
            if (seen.Add(cs2Root))
            {
                yield return cs2Root;
            }
        }
    }

    private static IEnumerable<string> GetSteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        libraries.Add(@"D:\SteamLibrary");
        libraries.Add(@"E:\SteamLibrary");

        foreach (var steamRoot in GetSteamRootCandidates())
        {
            var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
            {
                continue;
            }

            try
            {
                foreach (var line in File.ReadLines(vdfPath))
                {
                    var path = ExtractLibraryPath(line);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    libraries.Add(path);
                }
            }
            catch
            {
                return libraries;
            }
        }

        return libraries;
    }

    private static string? ExtractLibraryPath(string line)
    {
        var nestedMatch = NestedPathRegex.Match(line);
        if (nestedMatch.Success)
        {
            return UnescapePath(nestedMatch.Groups["path"].Value);
        }

        var legacyMatch = LegacyPathRegex.Match(line);
        if (legacyMatch.Success)
        {
            return UnescapePath(legacyMatch.Groups["path"].Value);
        }

        return null;
    }

    private static string UnescapePath(string path)
    {
        return path.Replace("\\\\", "\\");
    }

    private static IEnumerable<string> GetSteamRootCandidates()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Steam"));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Steam"));
        }

        roots.Add(@"C:\Steam");
        roots.Add(@"D:\Steam");
        roots.Add(@"E:\Steam");

        return roots;
    }

    private static string BuildCfgPath(string cs2Path)
    {
        return Path.Combine(cs2Path, "game", "csgo", "cfg", CfgFileName);
    }

    private async Task<string> BuildGsiUriAsync(CancellationToken ct)
    {
        var config = await _configurationService.GetAsync(ct);
        return BuildGsiUri(config.Gsi);
    }

    private static string BuildGsiUri(GsiConfig gsi)
    {
        var baseUrl = string.IsNullOrWhiteSpace(gsi.Url)
            ? "http://localhost:5292"
            : gsi.Url.Trim().TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(gsi.Path)
            ? "/gsi"
            : gsi.Path.Trim();

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return $"{baseUrl}{path}";
    }

    private static string BuildGsiConfigContent(string gsiUri)
    {
        return $$"""
        "UndefaultIt GSI"
        {
            "uri"           "{{gsiUri}}"
            "timeout"       "5.0"
            "buffer"        "0.1"
            "throttle"      "0.1"
            "heartbeat"     "30.0"
            "data"
            {
                "provider"              "1"
                "map"                   "1"
                "round"                 "1"
                "player_id"             "1"
                "player_state"          "1"
                "player_weapons"        "1"
                "player_match_stats"    "1"
            }
        }
        """;
    }

    private static async Task<bool> HasExpectedContentAsync(string cfgPath, string expectedContent, CancellationToken ct)
    {
        if (!File.Exists(cfgPath))
        {
            return false;
        }

        var currentContent = await File.ReadAllTextAsync(cfgPath, ct);
        return NormalizeLineEndings(currentContent) == NormalizeLineEndings(expectedContent);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Trim();
    }
}

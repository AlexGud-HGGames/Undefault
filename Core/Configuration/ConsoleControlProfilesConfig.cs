using System;
using System.Linq;

namespace Core.Configuration;

public static class MusicControlCommands
{
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Duck = "duck";
    public const string RestoreVolume = "restore_volume";

    public static string Normalize(string? command)
    {
        return string.IsNullOrWhiteSpace(command)
            ? string.Empty
            : command.Trim().ToLowerInvariant();
    }

    public static bool IsSupported(string command)
    {
        return string.Equals(command, Pause, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, Resume, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, Duck, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, RestoreVolume, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ConsoleControlProfilesConfig(
    string? ActiveProfileId,
    List<ConsoleControlProfile> Profiles
);

public sealed record ConsoleControlProfile(
    string Id,
    string Name,
    List<EventControlRule> Rules
)
{
    public EventControlRule? FindRule(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return null;
        }

        return Rules.FirstOrDefault(rule =>
            string.Equals(rule.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record EventControlRule(
    string EventKey,
    string Command,
    int? VolumePercent = null
);

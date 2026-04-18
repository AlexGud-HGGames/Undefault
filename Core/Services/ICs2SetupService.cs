namespace Core.Services;

public interface ICs2SetupService
{
    Task<Cs2SetupStatus> GetStatusAsync(CancellationToken ct = default);
    Task<Cs2SetupResult> InstallAsync(CancellationToken ct = default);
    Task<Cs2SetupResult> EnsureInstalledAsync(CancellationToken ct = default);
}

public record Cs2SetupStatus(
    bool IsCs2Found,
    string? Cs2Path,
    bool IsCfgInstalled,
    bool IsCfgCurrent,
    string? CfgPath,
    string? GsiUri,
    bool IsReady,
    string? Error
);

public record Cs2SetupResult(
    bool Success,
    string? CfgPath,
    string? GsiUri,
    bool WasUpdated,
    string? Error
);

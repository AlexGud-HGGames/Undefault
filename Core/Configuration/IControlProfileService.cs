namespace Core.Configuration;

public interface IControlProfileService
{
    string FilePath { get; }

    Task<ConsoleControlProfilesConfig> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ConsoleControlProfilesConfig config, CancellationToken cancellationToken = default);
}

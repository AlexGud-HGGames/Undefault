namespace Core.Configuration;

public interface IConfigurationService
{
    Task<SystemConfig> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SystemConfig config, CancellationToken cancellationToken = default);
}

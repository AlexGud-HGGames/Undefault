namespace Core.Configuration;

public interface IConfigurationService
{
    Task<AppConfig> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);
}

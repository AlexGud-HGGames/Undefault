namespace Core.Configuration;

public interface IProfileService
{
    Task<MusicProfilesConfig> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MusicProfilesConfig config, CancellationToken cancellationToken = default);
}

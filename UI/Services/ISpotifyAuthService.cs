using System.Threading;
using System.Threading.Tasks;

namespace UI.Services;

public interface ISpotifyAuthService
{
    Task<string> GetAuthorizationUrlAsync(CancellationToken cancellationToken = default);
}

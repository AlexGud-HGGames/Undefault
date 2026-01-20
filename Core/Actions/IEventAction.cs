using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace Core.Actions;

public interface IEventAction
{
    string Key { get; }
    Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default);
}

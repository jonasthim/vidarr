using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Health;

namespace Vidarr.Host;

/// <summary>
/// Resolves scoped IHealthCheck instances each time the singleton HealthMonitor
/// enumerates checks, so repository-backed checks get a fresh DbContext per run.
/// </summary>
internal sealed class HealthCheckResolver : IEnumerable<IHealthCheck>
{
    private readonly IServiceProvider _root;
    public HealthCheckResolver(IServiceProvider root) { _root = root; }

    public IEnumerator<IHealthCheck> GetEnumerator()
    {
        var scope = _root.CreateScope();
        try
        {
            foreach (var check in scope.ServiceProvider.GetServices<IHealthCheck>())
            {
                yield return check;
            }
        }
        finally
        {
            scope.Dispose();
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

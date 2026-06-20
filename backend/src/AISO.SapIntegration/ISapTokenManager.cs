using System.Threading;
using System.Threading.Tasks;

namespace AISO.SapIntegration;

public interface ISapTokenManager
{
    /// <summary>
    /// Gets a valid CSRF token. If a cached token exists, it returns it.
    /// Otherwise, it fetches a new one from SAP and caches it.
    /// </summary>
    Task<string> GetCsrfTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the CSRF token from SAP and updates the cache.
    /// </summary>
    Task<string> RefreshCsrfTokenAsync(CancellationToken cancellationToken = default);
}

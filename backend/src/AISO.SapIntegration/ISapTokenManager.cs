using System.Threading;
using System.Threading.Tasks;

namespace AISO.SapIntegration;

public record SapAuthContext(string CsrfToken, string SessionCookie);

public interface ISapTokenManager
{
    /// <summary>
    /// Gets a valid CSRF token and Session cookie. If cached context exists, it returns it.
    /// Otherwise, it fetches a new one from SAP and caches it.
    /// </summary>
    Task<SapAuthContext> GetAuthContextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the CSRF token and Session cookie from SAP and updates the cache.
    /// </summary>
    Task<SapAuthContext> RefreshAuthContextAsync(CancellationToken cancellationToken = default);
}

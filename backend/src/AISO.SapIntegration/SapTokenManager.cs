using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AISO.SapIntegration;

public class SapTokenManager : ISapTokenManager
{
    private const string CacheKey = "SapCsrfToken";
    
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SapTokenManager> _logger;

    public SapTokenManager(
        HttpClient httpClient, 
        IDistributedCache cache, 
        ILogger<SapTokenManager> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        var cachedToken = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedToken))
        {
            _logger.LogDebug("Retrieved SAP CSRF token from Redis cache");
            return cachedToken;
        }

        return await RefreshCsrfTokenAsync(cancellationToken);
    }

    public async Task<string> RefreshCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching new SAP CSRF token from server");
        
        // To get a CSRF token, we must send a GET request with header x-csrf-token: fetch
        var request = new HttpRequestMessage(HttpMethod.Get, "$metadata");
        request.Headers.Add("x-csrf-token", "fetch");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        // We do not throw if not success, because sometimes SAP returns 401/403 but STILL returns the CSRF token in headers.
        // However, usually we expect 200 OK for $metadata.
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("x-csrf-token", out var tokenValues))
        {
            var token = tokenValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                // Cache the token. Usually SAP tokens are valid for 30 minutes to 2 hours. We use 30 minutes to be safe.
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                
                await _cache.SetStringAsync(CacheKey, token, options, cancellationToken);
                _logger.LogInformation("Successfully fetched and cached new SAP CSRF token");
                
                return token;
            }
        }

        _logger.LogError("Failed to extract x-csrf-token from SAP response headers");
        throw new InvalidOperationException("Failed to fetch SAP CSRF token.");
    }
}

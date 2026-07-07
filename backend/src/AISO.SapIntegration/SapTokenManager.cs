using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AISO.SapIntegration;

public class SapTokenManager : ISapTokenManager
{
    private const string CacheKey = "SapCsrfContext";

    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<SapTokenManager> _logger;

    public SapTokenManager(
        HttpClient httpClient,
        ILogger<SapTokenManager> logger,
        IConnectionMultiplexer? redis = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _redis = redis;
    }

    public async Task<SapAuthContext> GetAuthContextAsync(CancellationToken cancellationToken = default)
    {
        if (_redis != null)
        {
            var db = _redis.GetDatabase();
            var cachedJson = await db.StringGetAsync(CacheKey);
            if (cachedJson.HasValue)
            {
                try
                {
                    var context = JsonSerializer.Deserialize<SapAuthContext>((string)cachedJson!);
                    if (context != null)
                    {
                        _logger.LogDebug("Retrieved SAP CSRF token from Redis cache");
                        return context;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached SAP auth context. Refreshing...");
                }
            }
        }

        return await RefreshAuthContextAsync(cancellationToken);
    }

    public async Task<SapAuthContext> RefreshAuthContextAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching new SAP CSRF token from server");

        var request = new HttpRequestMessage(HttpMethod.Get, "$metadata");
        request.Headers.Add("x-csrf-token", "fetch");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("x-csrf-token", out var tokenValues))
        {
            var token = tokenValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                var cookie = string.Empty;
                if (response.Headers.TryGetValues("set-cookie", out var cookieValues))
                {
                    // Basic handling: join all set-cookie values
                    cookie = string.Join("; ", cookieValues);
                }

                var context = new SapAuthContext(token, cookie);

                if (_redis != null)
                {
                    var db = _redis.GetDatabase();
                    await db.StringSetAsync(CacheKey, JsonSerializer.Serialize(context), TimeSpan.FromMinutes(30));
                }
                
                _logger.LogInformation("Successfully fetched and cached new SAP CSRF token and cookie");

                return context;
            }
        }

        _logger.LogError("Failed to extract x-csrf-token from SAP response headers");
        throw new InvalidOperationException("Failed to fetch SAP CSRF token.");
    }
}

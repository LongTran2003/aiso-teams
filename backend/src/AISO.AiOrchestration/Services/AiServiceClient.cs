using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AISO.AiOrchestration.Services;

/// <summary>
/// Configuration for the AI Orchestration microservice.
/// Bound from <c>AiService</c> config section.
/// </summary>
public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    /// <summary>Base URL of the AI Python microservice (e.g. http://localhost:8000).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>Timeout in seconds for a single orchestrate call.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// HTTP client for the AI orchestration microservice (Python/FastAPI).
/// Calls <c>POST /api/v1/orchestrate</c> with the user message and
/// returns the parsed <see cref="AiOrchestratorResponse"/>.
/// </summary>
public sealed class AiServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AiServiceClient> _logger;

    public AiServiceClient(HttpClient http, IOptions<AiServiceOptions> options, ILogger<AiServiceClient> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri(options.Value.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        _logger = logger;
    }

    /// <summary>
    /// Sends a user message to the AI service and returns the parsed response.
    /// </summary>
    public async Task<AiOrchestratorResponse> OrchestrateAsync(
        string userMessage, CancellationToken ct = default)
    {
        var payload = new { user_message = userMessage };

        _logger.LogInformation(
            "Calling AI service at {BaseUrl}/api/v1/orchestrate with message: {UserMessage}",
            _http.BaseAddress, userMessage);

        var response = await _http.PostAsJsonAsync("/api/v1/orchestrate", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "AI service returned {StatusCode}: {Body}",
                (int)response.StatusCode, body);

            throw new HttpRequestException(
                $"AI service returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AiOrchestratorResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

        if (result is null)
        {
            throw new InvalidOperationException("AI service returned null response");
        }

        _logger.LogInformation(
            "AI service returned intent={Intent}, tool_calls={ToolCallCount}",
            result.Intent, result.ToolCalls.Count);

        return result;
    }
}

using System.Text.Json.Serialization;

namespace AISO.AiOrchestration.Services;

/// <summary>
/// Maps to the ChatResponse returned by the AI microservice
/// (<c>POST /api/v1/orchestrate</c>).
/// </summary>
public sealed record AiOrchestratorResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; init; } = string.Empty;

    [JsonPropertyName("intent")]
    public string Intent { get; init; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = [];
}

public sealed record AiToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("function_name")]
    public string FunctionName { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; init; } = new();
}

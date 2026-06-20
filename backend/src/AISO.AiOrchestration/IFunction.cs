using System.Text.Json;

namespace AISO.AiOrchestration;

/// <summary>
/// Represents a single callable function exposed to the LLM for function calling.
/// Each function is self-describing: it advertises its name, description, and
/// parameter JSON schema to the LLM, and knows how to execute itself given
/// extracted parameters.
/// </summary>
public interface IFunction
{
    /// <summary>Function name as exposed to the LLM (e.g. "getSalesOrders").</summary>
    string Name { get; }

    /// <summary>Human-readable description used by the LLM to decide when to call.</summary>
    string Description { get; }

    /// <summary>JSON Schema (draft-07) describing the parameters object.</summary>
    string ParametersJsonSchema { get; }

    /// <summary>Execute the function with parameters extracted by the LLM.</summary>
    Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default);
}

/// <summary>
/// Result of executing a function. Carries either a typed payload or an error.
/// </summary>
public sealed record FunctionResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Typed payload (e.g. <see cref="IReadOnlyList{T}"/> of SalesOrder).</summary>
    public object? Payload { get; init; }

    public static FunctionResult Ok(object payload) =>
        new() { Success = true, Payload = payload };

    public static FunctionResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

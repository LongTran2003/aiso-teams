namespace AISO.AiOrchestration;

/// <summary>
/// Routes a user message to the appropriate function and returns the result.
/// In Sprint 2 this is keyword-based (<see cref="Stub.KeywordFunctionDispatcher"/>).
/// AI team replaces this with an Azure OpenAI function-calling dispatcher in Sprint 3
/// without changing this interface.
/// </summary>
public interface IFunctionDispatcher
{
    Task<DispatchResult> DispatchAsync(string userMessage, string requestingSapUser, CancellationToken ct = default);
}

public sealed record DispatchResult
{
    public required bool Handled { get; init; }
    public string? FunctionName { get; init; }
    public FunctionResult? Result { get; init; }

    /// <summary>Reason for non-handling (e.g. "intent unclear"), or null when handled.</summary>
    public string? Reason { get; init; }
}

namespace AISO.Persistence.Auditing;

/// <summary>
/// Writes audit entries for bot interactions. Used by the bot pipeline to
/// record every dispatched message for compliance and traceability.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Immutable payload describing a single bot interaction worth auditing.
/// Maps to a row in <c>audit_logs</c>.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Entra ID of the user (or "anonymous" in Emulator).</summary>
    public required string TeamsUserId { get; init; }

    /// <summary>Conversation ID from Bot Framework (may be null in some channels).</summary>
    public string? ConversationId { get; init; }

    /// <summary>Function name dispatched (e.g. "getSalesOrders"), or "unrecognized".</summary>
    public required string Action { get; init; }

    /// <summary>JSON of parameters passed to the function. Defaults to empty object.</summary>
    public string ParametersJson { get; init; } = "{}";

    /// <summary>"Success", "Failed", "Unrecognized".</summary>
    public string ResultStatus { get; init; } = "Success";

    /// <summary>Wall-clock duration of the dispatch in milliseconds.</summary>
    public int? DurationMs { get; init; }

    /// <summary>Error message if the call failed or was rejected.</summary>
    public string? ErrorMessage { get; init; }
}

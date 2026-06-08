namespace AISO.Persistence.Entities;

/// <summary>
/// Records each bot interaction for compliance and traceability.
/// Written by the bot pipeline after every dispatched function.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Entra ID of the user who initiated the action.</summary>
    public string TeamsUserId { get; set; } = string.Empty;

    /// <summary>Conversation ID from Bot Framework.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Function name (e.g. "getSalesOrders").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON-serialized parameters passed to the function.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>"Success", "Failed", "Cancelled".</summary>
    public string ResultStatus { get; set; } = "Success";

    /// <summary>Wall-clock duration of the function call in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Error message if the call failed.</summary>
    public string? ErrorMessage { get; set; }
}

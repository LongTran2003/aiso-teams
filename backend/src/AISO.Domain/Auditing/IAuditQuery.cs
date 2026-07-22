namespace AISO.Domain.Auditing;

/// <summary>Read-only access to bot audit logs (Admin).</summary>
public interface IAuditQuery
{
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken ct = default);
}

public sealed record AuditLogEntry
{
    public required long Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string TeamsUserId { get; init; }
    public required string Action { get; init; }
    public required string ResultStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public int? DurationMs { get; init; }
}

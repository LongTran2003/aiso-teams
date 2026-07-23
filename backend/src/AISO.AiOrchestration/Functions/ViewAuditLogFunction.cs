using System.Text.Json;
using AISO.Domain.Auditing;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>Admin-only: returns recent bot audit log entries.</summary>
public sealed class ViewAuditLogFunction : IFunction
{
    private readonly IAuditQuery _auditQuery;
    private readonly ILogger<ViewAuditLogFunction> _logger;

    public ViewAuditLogFunction(IAuditQuery auditQuery, ILogger<ViewAuditLogFunction> logger)
    {
        _auditQuery = auditQuery;
        _logger = logger;
    }

    public string Name => "ViewAuditLog";

    public string Description =>
        "Show the most recent bot audit log entries. Admin only.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "limit": {
              "type": "integer",
              "description": "Number of recent entries to return (default 20, max 100)."
            }
          },
          "required": []
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var limit = 20;
        if (parameters.TryGetProperty("limit", out var lim) && lim.TryGetInt32(out var parsed))
        {
            limit = parsed;
        }

        var entries = await _auditQuery.GetRecentAsync(limit, ct);
        _logger.LogInformation(
            "ViewAuditLog: user={User} returned {Count} entries",
            requestingSapUser, entries.Count);

        return FunctionResult.Ok(new ViewAuditLogResponse(
            entries.Count,
            $"Showing {entries.Count} recent audit log entries.",
            entries.Select(e => new AuditLogItem(
                e.Id,
                e.Timestamp.ToString("u"),
                e.TeamsUserId,
                e.Action,
                e.ResultStatus,
                e.DurationMs?.ToString() ?? "-",
                e.ErrorMessage ?? string.Empty)).ToList()));
    }
}

public sealed record AuditLogItem(
    long Id,
    string Timestamp,
    string TeamsUserId,
    string Action,
    string Status,
    string DurationMs,
    string Error);

public sealed record ViewAuditLogResponse(
    int Count,
    string Message,
    IReadOnlyList<AuditLogItem> Items);

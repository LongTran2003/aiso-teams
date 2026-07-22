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

        return FunctionResult.Ok(new
        {
            count = entries.Count,
            message = $"Showing {entries.Count} recent audit log entr{(entries.Count == 1 ? "y" : "ies")}.",
            items = entries.Select(e => new
            {
                id = e.Id,
                timestamp = e.Timestamp.ToString("u"),
                teams_user_id = e.TeamsUserId,
                action = e.Action,
                status = e.ResultStatus,
                duration_ms = e.DurationMs,
                error = e.ErrorMessage
            }).ToList()
        });
    }
}

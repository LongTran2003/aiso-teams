using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence.Auditing;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogger"/> that persists entries
/// to the <c>audit_logs</c> table via <see cref="AppDbContext"/>.
/// </summary>
public sealed class EfAuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;

    public EfAuditLogger(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var row = new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            TeamsUserId = entry.TeamsUserId,
            ConversationId = entry.ConversationId,
            Action = entry.Action,
            ParametersJson = entry.ParametersJson,
            ResultStatus = entry.ResultStatus,
            DurationMs = entry.DurationMs,
            ErrorMessage = entry.ErrorMessage
        };

        _db.AuditLogs.Add(row);
        await _db.SaveChangesAsync(ct);
    }
}

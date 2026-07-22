using AISO.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence.Auditing;

public sealed class EfAuditQuery : IAuditQuery
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EfAuditQuery(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        var limit = Math.Clamp(take, 1, 100);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .Select(a => new AuditLogEntry
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                TeamsUserId = a.TeamsUserId,
                Action = a.Action,
                ResultStatus = a.ResultStatus,
                ErrorMessage = a.ErrorMessage,
                DurationMs = a.DurationMs
            })
            .ToListAsync(ct);
    }
}

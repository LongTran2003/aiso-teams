using AISO.Domain.Approvals;
using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence.Approvals;

/// <summary>
/// EF Core implementation of <see cref="IOrderApprovalService"/>.
/// Uses <see cref="IDbContextFactory{TContext}"/> so it can be resolved from
/// singleton AI functions without capturing a scoped <see cref="AppDbContext"/>.
/// </summary>
public sealed class OrderApprovalService : IOrderApprovalService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public OrderApprovalService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<OrderApprovalRequest> RequestReleaseAsync(
        string soNumber,
        string requestedBySapUser,
        string? salesOrg,
        string? comment,
        CancellationToken ct = default)
    {
        var padded = PadSoNumber(soNumber);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.OrderApprovals
            .FirstOrDefaultAsync(
                a => a.SoNumber == padded && a.Status == ApprovalStatus.Pending,
                ct);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Sales order {padded} already has a pending release request " +
                $"(submitted by {existing.RequestedBySapUser}).");
        }

        var entity = new OrderApproval
        {
            SoNumber = padded,
            RequestedBySapUser = requestedBySapUser,
            SalesOrg = string.IsNullOrWhiteSpace(salesOrg) ? null : salesOrg.Trim().ToUpperInvariant(),
            Comment = comment,
            Status = ApprovalStatus.Pending,
            RequestedAt = DateTimeOffset.UtcNow
        };

        db.OrderApprovals.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToRequest(entity);
    }

    public async Task<OrderApprovalRequest?> GetPendingBySoNumberAsync(
        string soNumber,
        CancellationToken ct = default)
    {
        var padded = PadSoNumber(soNumber);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.OrderApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.SoNumber == padded && a.Status == ApprovalStatus.Pending,
                ct);

        return entity is null ? null : ToRequest(entity);
    }

    public async Task<OrderApprovalRequest?> GetLatestBySoNumberAsync(
        string soNumber,
        CancellationToken ct = default)
    {
        var padded = PadSoNumber(soNumber);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.OrderApprovals
            .AsNoTracking()
            .Where(a => a.SoNumber == padded)
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToRequest(entity);
    }

    public async Task<IReadOnlyList<OrderApprovalRequest>> GetPendingAsync(
        string? salesOrgFilter,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.OrderApprovals
            .AsNoTracking()
            .Where(a => a.Status == ApprovalStatus.Pending);

        if (!string.IsNullOrWhiteSpace(salesOrgFilter))
        {
            var org = salesOrgFilter.Trim().ToUpperInvariant();
            query = query.Where(a => a.SalesOrg == null || a.SalesOrg == org);
        }

        var list = await query
            .OrderBy(a => a.RequestedAt)
            .ToListAsync(ct);

        return list.Select(ToRequest).ToList();
    }

    public Task<OrderApprovalRequest> ApproveAsync(
        string soNumber,
        string decidedBySapUser,
        string? managerSalesOrg,
        bool isAdmin,
        string? comment,
        CancellationToken ct = default) =>
        DecideAsync(soNumber, decidedBySapUser, managerSalesOrg, isAdmin, comment, ApprovalStatus.Approved, ct);

    public Task<OrderApprovalRequest> RejectAsync(
        string soNumber,
        string decidedBySapUser,
        string? managerSalesOrg,
        bool isAdmin,
        string? comment,
        CancellationToken ct = default) =>
        DecideAsync(soNumber, decidedBySapUser, managerSalesOrg, isAdmin, comment, ApprovalStatus.Rejected, ct);

    private async Task<OrderApprovalRequest> DecideAsync(
        string soNumber,
        string decidedBySapUser,
        string? managerSalesOrg,
        bool isAdmin,
        string? comment,
        ApprovalStatus decision,
        CancellationToken ct)
    {
        var padded = PadSoNumber(soNumber);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.OrderApprovals
            .FirstOrDefaultAsync(
                a => a.SoNumber == padded && a.Status == ApprovalStatus.Pending,
                ct);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"No pending release request found for sales order {padded}.");
        }

        if (!isAdmin
            && !string.IsNullOrWhiteSpace(managerSalesOrg)
            && !string.IsNullOrWhiteSpace(entity.SalesOrg)
            && !string.Equals(entity.SalesOrg, managerSalesOrg, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Order {padded} belongs to sales org {entity.SalesOrg}; " +
                $"your scope is {managerSalesOrg}.");
        }

        entity.Status = decision;
        entity.DecidedBySapUser = decidedBySapUser;
        entity.DecisionComment = comment;
        entity.DecidedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToRequest(entity);
    }

    private static string PadSoNumber(string soNumber)
    {
        var trimmed = soNumber.Trim();
        return trimmed.All(char.IsDigit) && trimmed.Length < 10
            ? trimmed.PadLeft(10, '0')
            : trimmed;
    }

    private static OrderApprovalRequest ToRequest(OrderApproval e) => new()
    {
        Id = e.Id,
        SoNumber = e.SoNumber,
        RequestedBySapUser = e.RequestedBySapUser,
        SalesOrg = e.SalesOrg,
        Comment = e.Comment,
        Status = e.Status,
        DecidedBySapUser = e.DecidedBySapUser,
        DecisionComment = e.DecisionComment,
        RequestedAt = e.RequestedAt,
        DecidedAt = e.DecidedAt
    };
}

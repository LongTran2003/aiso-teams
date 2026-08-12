using AISO.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence.Users;

public sealed class UserScopeLookup : IUserScopeLookup
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public UserScopeLookup(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UserRole> GetRoleBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var mapping = await db.UserMappings
            .AsNoTracking()
            .Where(u => u.SapUserId == sapUserId)
            .OrderByDescending(u => u.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        return mapping?.Role ?? UserRole.Employee;
    }

    public async Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var salesOrg = await db.UserMappings
            .AsNoTracking()
            .Where(u => u.SapUserId == sapUserId)
            .OrderByDescending(u => u.UpdatedAt)
            .Select(u => u.SalesOrg)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(salesOrg) ? null : salesOrg;
    }

    public async Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var delegatedBy = await db.UserMappings
            .AsNoTracking()
            .Where(u => u.SapUserId == sapUserId)
            .OrderByDescending(u => u.UpdatedAt)
            .Select(u => u.DelegatedBySapUser)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(delegatedBy) ? null : delegatedBy;
    }
}

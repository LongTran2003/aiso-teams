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

        if (mapping is not null)
            return mapping.Role;

        var assignment = await db.SapLinkAssignments
            .AsNoTracking()
            .Where(a => a.SapUserId == sapUserId)
            .FirstOrDefaultAsync(ct);

        return assignment?.Role ?? UserRole.Employee;
    }

    public async Task<string?> GetSalesOrgBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var mapping = await db.UserMappings
            .AsNoTracking()
            .Where(u => u.SapUserId == sapUserId)
            .OrderByDescending(u => u.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        return mapping?.SalesOrg;
    }

    public async Task<string?> GetDelegatedBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        
        var delegatedBy = await db.UserMappings
            .Where(u => u.SapUserId == sapUserId)
            .Select(u => u.DelegatedBySapUser)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(delegatedBy))
        {
            delegatedBy = await db.SapLinkAssignments
                .Where(a => a.SapUserId == sapUserId)
                .Select(a => a.DelegatedBySapUser)
                .FirstOrDefaultAsync(ct);
        }

        return string.IsNullOrWhiteSpace(delegatedBy) ? null : delegatedBy;
    }

    public async Task<string?> GetEmailBySapUserAsync(string sapUserId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var email = await db.SapLinkAssignments
            .Where(a => a.SapUserId == sapUserId)
            .Select(a => a.TeamsEmail)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    public async Task SetDelegatedBySapUserAsync(string delegateUser, string? delegatorUser, DateTimeOffset? validTo = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        
        var mappings = await db.UserMappings
            .Where(u => u.SapUserId == delegateUser)
            .ToListAsync(ct);

        foreach (var mapping in mappings)
        {
            mapping.DelegatedBySapUser = delegatorUser;
            mapping.DelegatedValidTo = validTo;
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var assignments = await db.SapLinkAssignments
            .Where(a => a.SapUserId == delegateUser)
            .ToListAsync(ct);

        foreach (var assignment in assignments)
        {
            assignment.DelegatedBySapUser = delegatorUser;
            assignment.DelegatedValidTo = validTo;
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (mappings.Count > 0 || assignments.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}

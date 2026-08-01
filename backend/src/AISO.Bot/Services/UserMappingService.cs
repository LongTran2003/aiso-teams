using AISO.Domain.Users;
using AISO.Persistence;
using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Bot.Services;

public class UserMappingService
{
    private readonly AppDbContext _dbContext;

    public UserMappingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetSapUsernameAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return mapping?.SapUserId;
    }

    public async Task<IReadOnlyList<(string Title, string Value)>> GetForwardRecipientChoicesAsync(CancellationToken cancellationToken = default)
    {
        var mappings = await _dbContext.UserMappings
            .Where(u => !string.IsNullOrWhiteSpace(u.SapUserId))
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.SapUserId)
            .Select(u => new { u.DisplayName, u.SapUserId })
            .ToListAsync(cancellationToken);

        // Value is the SAP User ID: it is the canonical order owner stored in
        // zaiso_so_map and fits SAP's 50-char field (Teams IDs are too long).
        return mappings
            .Select(mapping => (
                Title: string.IsNullOrWhiteSpace(mapping.DisplayName)
                    ? mapping.SapUserId!
                    : $"{mapping.DisplayName} ({mapping.SapUserId})",
                Value: mapping.SapUserId!))
            .ToList();
    }

    /// <summary>
    /// Returns the RBAC role for a user. Unknown/unmapped users default to
    /// <see cref="UserRole.Employee"/> (least privilege).
    /// </summary>
    public async Task<UserRole> GetRoleAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return mapping?.Role ?? UserRole.Employee;
    }

    /// <summary>Sales organization (VKORG) scope for Manager filtering. Null if unset.</summary>
    public async Task<string?> GetSalesOrgAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(mapping?.SalesOrg) ? null : mapping.SalesOrg;
    }

    public async Task<string?> GetDisplayNameAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(mapping?.DisplayName) ? mapping?.SapUserId : mapping.DisplayName;
    }

    /// <summary>
    /// Finds the admin assignment for this Teams identity (by Teams user id or email).
    /// </summary>
    public async Task<SapLinkAssignment?> FindLinkAssignmentAsync(
        string teamsUserId,
        string? teamsEmail,
        CancellationToken cancellationToken = default)
    {
        var byTeamsId = await _dbContext.SapLinkAssignments
            .FirstOrDefaultAsync(a => a.TeamsUserId == teamsUserId, cancellationToken);
        if (byTeamsId is not null)
            return byTeamsId;

        if (string.IsNullOrWhiteSpace(teamsEmail))
            return null;

        var email = NormalizeEmail(teamsEmail);
        return await _dbContext.SapLinkAssignments
            .FirstOrDefaultAsync(a => a.TeamsEmail == email, cancellationToken);
    }

    public async Task<bool> IsSapUserLinkedToOtherTeamsUserAsync(
        string sapUserId,
        string teamsUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = sapUserId.Trim().ToUpperInvariant();
        return await _dbContext.UserMappings.AnyAsync(
            u => u.SapUserId == normalized
                 && u.TeamsUserId != teamsUserId,
            cancellationToken);
    }

    public async Task MapUserAsync(
        string teamsUserId,
        string displayName,
        string sapUserId,
        CancellationToken cancellationToken = default,
        UserRole? role = null,
        string? salesOrg = null)
    {
        var normalizedSap = sapUserId.Trim().ToUpperInvariant();
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping == null)
        {
            mapping = new UserMapping
            {
                TeamsUserId = teamsUserId,
                DisplayName = displayName,
                SapUserId = normalizedSap,
                Role = role ?? UserRole.Employee,
                SalesOrg = NormalizeSalesOrg(salesOrg),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.UserMappings.Add(mapping);
        }
        else
        {
            mapping.DisplayName = displayName;
            mapping.SapUserId = normalizedSap;
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
            if (role.HasValue)
                mapping.Role = role.Value;
            if (salesOrg is not null || role.HasValue)
                mapping.SalesOrg = NormalizeSalesOrg(salesOrg);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Bind Teams user id onto the assignment after a successful link.</summary>
    public async Task BindAssignmentTeamsUserAsync(
        SapLinkAssignment assignment,
        string teamsUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(assignment.TeamsUserId, teamsUserId, StringComparison.Ordinal))
            return;

        assignment.TeamsUserId = teamsUserId;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMappingAsync(string teamsUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping != null)
        {
            _dbContext.UserMappings.Remove(mapping);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormalizeSalesOrg(string? salesOrg) =>
        string.IsNullOrWhiteSpace(salesOrg) ? null : salesOrg.Trim().ToUpperInvariant();
}

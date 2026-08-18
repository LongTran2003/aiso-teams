using AISO.Domain.Users;
using AISO.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISO.Persistence.Users;

public sealed class BotUserAdminService : IBotUserAdminService
{
    private static readonly HashSet<string> KnownSalesOrgs = new(StringComparer.OrdinalIgnoreCase)
    {
        "TV01", "FU24", "UE00", "UW00", "DN00", "DS00"
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public BotUserAdminService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<BotUserSummary>> ListLinkedUsersAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var mappings = await db.UserMappings
            .Where(u => !string.IsNullOrWhiteSpace(u.SapUserId))
            .ToListAsync(ct);

        var assignments = await db.SapLinkAssignments
            .ToListAsync(ct);

        var assignedSapIds = assignments
            .Select(a => a.SapUserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mappedSapIds = mappings
            .Select(m => m.SapUserId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summaries = new List<BotUserSummary>();

        foreach (var m in mappings)
        {
            summaries.Add(ToSummary(m, assignedSapIds.Contains(m.SapUserId!)));
        }

        foreach (var a in assignments)
        {
            if (!mappedSapIds.Contains(a.SapUserId))
            {
                summaries.Add(new BotUserSummary(
                    a.SapUserId,
                    a.TeamsEmail ?? a.SapUserId,
                    a.Role,
                    a.SalesOrg,
                    true));
            }
        }

        return summaries
            .OrderBy(s => s.DisplayName)
            .ThenBy(s => s.SapUserId)
            .ToList();
    }

    public async Task<BotUserSummary?> GetBySapUserIdAsync(string sapUserId, CancellationToken ct = default)
    {
        var normalized = NormalizeSap(sapUserId);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var mapping = await db.UserMappings
            .FirstOrDefaultAsync(u => u.SapUserId == normalized, ct);
        if (mapping is null)
            return null;

        var hasAssignment = await db.SapLinkAssignments
            .AnyAsync(a => a.SapUserId == normalized, ct);

        return ToSummary(mapping, hasAssignment);
    }

    public async Task<BotUserSummary> UpdateAccessAsync(
        string sapUserId,
        UserRole role,
        string? salesOrg,
        CancellationToken ct = default)
    {
        var normalized = NormalizeSap(sapUserId);
        var org = NormalizeSalesOrg(salesOrg, role);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var mapping = await db.UserMappings
            .FirstOrDefaultAsync(u => u.SapUserId == normalized, ct)
            ?? throw new InvalidOperationException(
                $"No linked Teams user found for SAP ID {normalized}. User must link the bot first.");

        mapping.Role = role;
        mapping.SalesOrg = org;
        mapping.UpdatedAt = DateTimeOffset.UtcNow;

        var assignment = await db.SapLinkAssignments
            .FirstOrDefaultAsync(a => a.SapUserId == normalized, ct);
        if (assignment is not null)
        {
            assignment.Role = role;
            assignment.SalesOrg = org;
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return ToSummary(mapping, assignment is not null);
    }

    public async Task<BotUserSummary> PreAssignAccessAsync(
        string sapUserId,
        string teamsEmail,
        UserRole role,
        string? salesOrg,
        CancellationToken ct = default)
    {
        var normalizedSap = NormalizeSap(sapUserId);
        var normalizedEmail = teamsEmail.Trim().ToLowerInvariant();
        var org = NormalizeSalesOrg(salesOrg, role);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existingBySap = await db.SapLinkAssignments
            .FirstOrDefaultAsync(a => a.SapUserId == normalizedSap, ct);
        if (existingBySap is not null && existingBySap.TeamsEmail != normalizedEmail)
        {
            throw new InvalidOperationException($"SAP ID {normalizedSap} is already assigned to a different email ({existingBySap.TeamsEmail}).");
        }

        var existingByEmail = await db.SapLinkAssignments
            .FirstOrDefaultAsync(a => a.TeamsEmail == normalizedEmail, ct);
        if (existingByEmail is not null && existingByEmail.SapUserId != normalizedSap)
        {
            throw new InvalidOperationException($"Email {normalizedEmail} is already assigned to a different SAP ID ({existingByEmail.SapUserId}).");
        }

        if (existingBySap is not null)
        {
            existingBySap.Role = role;
            existingBySap.SalesOrg = org;
            existingBySap.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.SapLinkAssignments.Add(new SapLinkAssignment
            {
                SapUserId = normalizedSap,
                TeamsEmail = normalizedEmail,
                Role = role,
                SalesOrg = org,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);

        return new BotUserSummary(
            normalizedSap,
            normalizedSap, // No display name yet
            role,
            org,
            true);
    }

    private static BotUserSummary ToSummary(UserMapping mapping, bool hasAssignment) =>
        new(
            mapping.SapUserId ?? string.Empty,
            string.IsNullOrWhiteSpace(mapping.DisplayName)
                ? mapping.SapUserId ?? string.Empty
                : mapping.DisplayName,
            mapping.Role,
            mapping.SalesOrg,
            hasAssignment);

    private static string NormalizeSap(string sapUserId) =>
        sapUserId.Trim().ToUpperInvariant();

    /// <summary>
    /// Admin → SalesOrg cleared. Manager should have an org; empty clears and lets Admin fix later.
    /// Unknown org codes are rejected.
    /// </summary>
    internal static string? NormalizeSalesOrg(string? salesOrg, UserRole role)
    {
        if (role == UserRole.Admin)
            return null;

        if (string.IsNullOrWhiteSpace(salesOrg)
            || string.Equals(salesOrg.Trim(), "(none)", StringComparison.OrdinalIgnoreCase)
            || string.Equals(salesOrg.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(salesOrg.Trim(), "-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var org = salesOrg.Trim().ToUpperInvariant();
        if (!KnownSalesOrgs.Contains(org))
        {
            throw new InvalidOperationException(
                $"Unknown sales org '{salesOrg}'. Use TV01, FU24, UE00, UW00, DN00, DS00, or leave empty.");
        }

        return org;
    }
}

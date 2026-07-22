using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task MapUserAsync(string teamsUserId, string displayName, string sapUserId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.UserMappings
            .Where(u => u.TeamsUserId == teamsUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping == null)
        {
            mapping = new UserMapping
            {
                TeamsUserId = teamsUserId,
                DisplayName = displayName,
                SapUserId = sapUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.UserMappings.Add(mapping);
        }
        else
        {
            mapping.DisplayName = displayName;
            mapping.SapUserId = sapUserId;
            mapping.UpdatedAt = DateTimeOffset.UtcNow;
        }

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
}
